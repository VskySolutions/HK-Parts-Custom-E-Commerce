using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Advances an order along the WO-45 fulfilment lifecycle
/// (Pending → Processing → Shipped → Delivered, plus Cancelled from any non-terminal state).
/// Every transition is validated against the state machine, appended to the immutable
/// <see cref="OrderStatusHistory"/> (AC-ORD-001.3), and — on Shipped/Delivered — triggers a
/// customer notification email (AC-ORD-001.5). Shipping requires a tracking number and carrier
/// (AC-ORD-001.4), both of which are persisted on the order.
/// </summary>
public record AdvanceOrderStatusCommand(Guid OrderId, string ToStatus, string? TrackingNumber, string? Carrier)
    : IRequest<OrderDto>;

public class AdvanceOrderStatusCommandHandler : IRequestHandler<AdvanceOrderStatusCommand, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailTemplateSender _templates;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly IPaymentGatewayRouter _payments;
    private readonly IInventoryService _inventory;

    public AdvanceOrderStatusCommandHandler(
        IApplicationDbContext db,
        IEmailTemplateSender templates,
        ICurrentUserService current,
        IDateTimeProvider clock,
        IPaymentGatewayRouter payments,
        IInventoryService inventory)
    {
        _db = db;
        _templates = templates;
        _current = current;
        _clock = clock;
        _payments = payments;
        _inventory = inventory;
    }

    public async Task<OrderDto> Handle(AdvanceOrderStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrderStatus>(request.ToStatus, ignoreCase: true, out var target))
            throw new ConflictException(
                $"'{request.ToStatus}' is not a recognised order status. " +
                "Allowed lifecycle targets: Processing, Shipped, Delivered, Cancelled.");

        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        var from = order.Status;
        if (!IsValidTransition(from, target))
            throw new ConflictException($"An order in status '{from}' cannot transition to '{target}'.");

        // Shipping requires tracking details, captured on the order (AC-ORD-001.4).
        if (target == OrderStatus.Shipped)
        {
            if (string.IsNullOrWhiteSpace(request.TrackingNumber) || string.IsNullOrWhiteSpace(request.Carrier))
                throw new ConflictException("Shipping an order requires both a tracking number and a carrier.");

            order.TrackingNumber = request.TrackingNumber;
            order.ShippingCarrier = request.Carrier;
        }

        var now = _clock.UtcNow;
        order.Status = target;

        if (target == OrderStatus.Shipped)
            order.ShippedOnUtc = now;
        else if (target == OrderStatus.Delivered)
            order.DeliveredOnUtc = now;

        // Immutable audit entry for the transition (AC-ORD-001.3).
        _db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = from,
            ToStatus = target,
            ChangedById = _current.UserId,
            ChangedOnUtc = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Cancelling an order whose stock was committed at placement returns that stock (AC-CAT-011.5) —
        // the reverse of the single placement decrement. The committed states are exactly the ones that
        // reach the checkout's finalize step: Captured/Authorized (card gateways) and AwaitingPayment
        // (COD/bank transfer). A still-unpaid Pending redirect order never decremented, so it's excluded;
        // a refunded order was already restocked by the refund path, so it's excluded to avoid a double.
        if (target == OrderStatus.Cancelled &&
            order.PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Captured or PaymentStatus.AwaitingPayment)
            await _inventory.RestoreForOrderAsync(order, ct: cancellationToken);

        // Auto-capture an authorize-only payment when the order ships (AC-PAY-002.3). No-op for orders
        // paid up front, COD/bank-transfer, or already captured — the router only captures an open
        // Authorized hold. Runs after the status is persisted so a capture failure can't block shipping.
        if (target == OrderStatus.Shipped)
            await _payments.CaptureForOrderAsync(order.Id, cancellationToken);

        // Notify the customer of every lifecycle change — Processing / Shipped / Delivered / Cancelled
        // (AC-ORD-001.5) — using the admin-editable email template for the event.
        var note = BuildStatusNotification(order, target);
        if (note is not null)
        {
            await _templates.SendAsync(note.Value.Key, order.ContactEmail ?? string.Empty, order.ContactName,
                note.Value.Vars, cancellationToken);
        }

        return OrderDto.From(order);
    }

    /// <summary>Template key + variables for the customer notification of a lifecycle transition.</summary>
    private static (string Key, Dictionary<string, string> Vars)? BuildStatusNotification(Order order, OrderStatus target)
    {
        var name = string.IsNullOrWhiteSpace(order.ContactName) ? "there" : order.ContactName!;
        Dictionary<string, string> Base() => new()
        {
            ["customerName"] = name,
            ["orderNumber"] = order.OrderNumber,
        };

        return target switch
        {
            OrderStatus.Processing => ("order.processing", Base()),

            OrderStatus.Shipped => ("order.shipped", Merge(Base(), new()
            {
                ["carrier"] = order.ShippingCarrier ?? string.Empty,
                ["trackingNumber"] = order.TrackingNumber ?? string.Empty,
                ["trackingUrl"] = string.Empty,
            })),

            OrderStatus.Delivered => ("order.delivered", Merge(Base(), new()
            {
                ["deliveredAt"] = order.DeliveredOnUtc?.ToString("MMM d, yyyy") ?? string.Empty,
            })),

            OrderStatus.Cancelled => ("order.cancelled", Base()),

            _ => null,
        };
    }

    private static Dictionary<string, string> Merge(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        foreach (var kv in b) a[kv.Key] = kv.Value;
        return a;
    }

    /// <summary>
    /// The lifecycle state machine: a linear Pending → Processing → Shipped → Delivered progression,
    /// with Cancelled reachable from any non-terminal state (Delivered and Cancelled are terminal).
    /// </summary>
    private static bool IsValidTransition(OrderStatus from, OrderStatus to) => to switch
    {
        OrderStatus.Processing => from == OrderStatus.Pending,
        OrderStatus.Shipped => from == OrderStatus.Processing,
        OrderStatus.Delivered => from == OrderStatus.Shipped,
        OrderStatus.Cancelled => from is OrderStatus.Pending or OrderStatus.Processing or OrderStatus.Shipped,
        _ => false,
    };
}
