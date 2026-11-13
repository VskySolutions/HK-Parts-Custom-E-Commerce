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
    private readonly GiftCertificates.IGiftCertificateService? _giftCertificates;

    public AdvanceOrderStatusCommandHandler(
        IApplicationDbContext db,
        IEmailTemplateSender templates,
        ICurrentUserService current,
        IDateTimeProvider clock,
        IPaymentGatewayRouter payments,
        IInventoryService inventory,
        GiftCertificates.IGiftCertificateService? giftCertificates = null)
    {
        _db = db;
        _templates = templates;
        _current = current;
        _clock = clock;
        _payments = payments;
        _inventory = inventory;
        _giftCertificates = giftCertificates;
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

        // What the order had committed before this transition decides the restock below (a cancel's reversal
        // changes PaymentStatus to Voided/Refunded first).
        var paidBefore = order.PaymentStatus;

        // AN-24: cancelling releases the money first: the gateway voids an unsettled transaction or refunds a
        // settled one. If it refuses, nothing is cancelled and the admin sees the gateway's reason.
        if (target == OrderStatus.Cancelled)
            await ReversePaymentsAsync(order, now, cancellationToken);

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
            paidBefore is PaymentStatus.Authorized or PaymentStatus.Captured or PaymentStatus.AwaitingPayment
                or PaymentStatus.HeldForReview)
            await _inventory.RestoreForOrderAsync(order, ct: cancellationToken);

        // Gift certificates: give back what this order redeemed, and void the ones it purchased (unused only;
        // a partly spent one flags the order for manual review).
        if (target == OrderStatus.Cancelled && _giftCertificates is not null)
            await _giftCertificates.HandleOrderCancelledAsync(order.Id, cancellationToken);

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

    /// <summary>
    /// Reverses each open gateway payment of a cancelled order via its gateway's automatic reversal (Authorize.Net:
    /// void when unsettled, refund when settled). Gateways without one return null and are left for a manual
    /// refund, as before. Gift-certificate payments are restored by the gift-certificate service instead.
    /// </summary>
    private async Task ReversePaymentsAsync(Order order, DateTime now, CancellationToken ct)
    {
        var open = await _db.PaymentRecords
            .Where(p => p.OrderId == order.Id
                        && p.Method != PaymentMethodType.GiftCertificate
                        && (p.Status == PaymentStatus.Authorized || p.Status == PaymentStatus.Captured
                            || p.Status == PaymentStatus.PartiallyRefunded || p.Status == PaymentStatus.HeldForReview))
            .ToListAsync(ct);

        foreach (var payment in open)
        {
            var outstanding = payment.Amount - payment.RefundedAmount;
            if (outstanding <= 0) continue;

            var result = await _payments.ReverseAsync(payment, ct);
            if (result is null) continue;

            Payments.PaymentLedger.Record(_db, payment, PaymentTransactionType.Refund, outstanding, result, now, _current.UserId);
            if (!result.Success)
            {
                payment.ErrorMessage = result.ErrorMessage;
                await _db.SaveChangesAsync(ct);
                throw new ConflictException($"The order was not cancelled because the payment could not be reversed: {result.ErrorMessage}");
            }

            payment.RefundedAmount = payment.Amount;
            payment.RefundedOnUtc = now;
            payment.AuthorizationExpiresUtc = null;
            payment.ErrorMessage = null;
            payment.Status = result.Status == PaymentStatus.Voided ? PaymentStatus.Voided : PaymentStatus.Refunded;
            if (payment.IsMaster)
                order.PaymentStatus = payment.Status;
        }
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
        // A Payment Review order is released by the gateway status sync, never by hand; it can only be cancelled.
        OrderStatus.Cancelled => from is OrderStatus.Pending or OrderStatus.Processing or OrderStatus.Shipped
            or OrderStatus.PaymentReview,
        _ => false,
    };
}
