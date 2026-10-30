using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>
/// Approves or rejects a return (AC-ORD-004.3). On approval it restocks accepted units at the fulfilling
/// store and, when the resolution is a refund, processes it through the order's original gateway
/// (AC-ORD-004.4). Replacement / store-credit resolutions are recorded (downstream flows are future work).
/// </summary>
public record ResolveRmaCommand(
    Guid RmaId,
    bool Approve,
    RmaResolution Resolution = RmaResolution.None,
    decimal? RefundAmount = null,
    string? Notes = null) : IRequest<RmaDto>;

public class ResolveRmaCommandHandler : IRequestHandler<ResolveRmaCommand, RmaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IInventoryService _inventory;
    private readonly IStoreCreditService _storeCredit;
    private readonly ISender _mediator;
    private readonly IEmailTemplateSender _templates;
    private readonly IDateTimeProvider _clock;

    public ResolveRmaCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, IInventoryService inventory,
        IStoreCreditService storeCredit, ISender mediator, IEmailTemplateSender templates, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _inventory = inventory;
        _storeCredit = storeCredit;
        _mediator = mediator;
        _templates = templates;
        _clock = clock;
    }

    public async Task<RmaDto> Handle(ResolveRmaCommand request, CancellationToken cancellationToken)
    {
        var rma = await _db.Rmas
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.RmaId, cancellationToken)
            ?? throw new NotFoundException(nameof(Rma), request.RmaId);

        if (rma.Status != RmaStatus.Requested)
            throw new ConflictException($"This return is already {rma.Status} and cannot be resolved again.");

        var order = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == rma.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), rma.OrderId);

        var now = _clock.UtcNow;

        if (!request.Approve)
        {
            rma.Status = RmaStatus.Rejected;
            rma.Resolution = RmaResolution.None;
            rma.ResolutionNotes = request.Notes;
            rma.ResolvedOnUtc = now;
            rma.ResolvedById = _current.UserId;
            await _db.SaveChangesAsync(cancellationToken);
            await NotifyAsync(order, rma, approved: false, cancellationToken);
            return RmaDto.From(rma);
        }

        // Approved: restock accepted units at the fulfilling store (the only return flow that adds stock).
        if (order.AssignedStoreId is Guid storeId)
        {
            foreach (var line in rma.Lines)
                await _inventory.MarkAsReceivedAsync(line.ProductId, line.ProductVariantId, storeId, line.Quantity, cancellationToken);
        }

        // Resolution-specific downstream flow (AC-ORD-004.3/004.4).
        switch (request.Resolution)
        {
            case RmaResolution.Refund:
            {
                var amount = request.RefundAmount ?? ReturnedValue(order, rma);
                if (amount > 0)
                {
                    // NotifyCustomer: false — the RmaResolved email below already informs the buyer, so the
                    // refund handler must not send a second (duplicate) email for the same action.
                    // RestockItems: false — accepted units were already restocked above via MarkAsReceivedAsync,
                    // so the refund must not restock them a second time.
                    await _mediator.Send(new RefundOrderCommand(order.Id, null, amount, $"RMA {rma.RmaNumber}", NotifyCustomer: false, RestockItems: false), cancellationToken);
                    rma.RefundedAmount = amount;
                }
                break;
            }
            case RmaResolution.StoreCredit:
            {
                // Issue the returned value as store credit to the buyer's ledger balance.
                var amount = request.RefundAmount ?? ReturnedValue(order, rma);
                if (amount > 0)
                {
                    await _storeCredit.IssueAsync(
                        rma.CustomerId, amount, order.CurrencyCode,
                        $"Store credit for return {rma.RmaNumber}", rma.Id, order.Id, cancellationToken);
                    rma.StoreCreditIssued = amount;
                }
                break;
            }
            case RmaResolution.Replacement:
            {
                // Create a no-charge replacement order for the returned units, fulfilled from the same store.
                var replacement = CreateReplacementOrder(order, rma, now);
                _db.Orders.Add(replacement);
                rma.ReplacementOrderId = replacement.Id;
                break;
            }
        }

        rma.Status = RmaStatus.Completed;
        rma.Resolution = request.Resolution;
        rma.ResolutionNotes = request.Notes;
        rma.ResolvedOnUtc = now;
        rma.ResolvedById = _current.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyAsync(order, rma, approved: true, cancellationToken);
        return RmaDto.From(rma);
    }

    /// <summary>The monetary value of the returned units, from the original order lines' unit prices.</summary>
    private static decimal ReturnedValue(Order order, Domain.Entities.Rma rma)
    {
        var unitPrices = order.Lines.ToDictionary(l => l.Id, l => l.UnitPrice);
        return rma.Lines.Sum(l => (unitPrices.TryGetValue(l.OrderLineItemId, out var up) ? up : 0m) * l.Quantity);
    }

    /// <summary>
    /// Builds a no-charge replacement order for the returned units, copying the original order's delivery
    /// target and fulfilling store. It enters the normal fulfilment lifecycle at Processing.
    /// </summary>
    private static Order CreateReplacementOrder(Order order, Domain.Entities.Rma rma, DateTime now)
    {
        var seq = rma.RmaNumber.Split('-').LastOrDefault() ?? "1";
        var replacement = new Order
        {
            OrderNumber = $"{order.OrderNumber}-R{seq}",
            CustomerId = order.CustomerId,
            Status = OrderStatus.Processing,
            PaymentStatus = PaymentStatus.Captured, // no-charge: the original order already paid
            ShippingAddressId = order.ShippingAddressId,
            AssignedStoreId = order.AssignedStoreId,
            IsPickup = order.IsPickup,
            CurrencyCode = order.CurrencyCode,
            Subtotal = 0m,
            DiscountTotal = 0m,
            ShippingTotal = 0m,
            TaxTotal = 0m,
            TotalAmount = 0m,
            PlacedOnUtc = now,
            RoutedOnUtc = order.AssignedStoreId is null ? null : now,
        };

        // A replacement has to be made to the same spec as the original, so the buyer's typed values
        // (engraving, gift note, …) come across from the order line being returned.
        var customisations = order.Lines.ToDictionary(l => l.Id, l => l.CustomAttributesJson);

        foreach (var line in rma.Lines)
        {
            replacement.Lines.Add(new OrderLineItem
            {
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                Quantity = line.Quantity,
                UnitPrice = 0m,
                OriginalUnitPrice = 0m,
                LineTotal = 0m,
                CustomAttributesJson = customisations.TryGetValue(line.OrderLineItemId, out var json) ? json : null,
            });
        }

        return replacement;
    }

    private async Task NotifyAsync(Order order, Domain.Entities.Rma rma, bool approved, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>
        {
            ["customerName"] = string.IsNullOrWhiteSpace(order.ContactName) ? "there" : order.ContactName!,
            ["orderNumber"] = order.OrderNumber,
            ["rmaNumber"] = rma.RmaNumber,
            ["resolution"] = rma.Resolution.ToString(),
            ["rejectionReason"] = string.IsNullOrWhiteSpace(rma.ResolutionNotes) ? "it did not meet our return policy" : rma.ResolutionNotes!,
        };
        var key = approved ? "return.approved" : "return.rejected";
        await _templates.SendAsync(key, order.ContactEmail ?? string.Empty, order.ContactName, vars, ct);
    }
}
