using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>
/// Reads a payment's current state from its gateway and applies it (Authorize.Net AN-16). This is how an order
/// held for fraud review leaves "Payment Review": the merchant approves or declines the transaction in the
/// Authorize.Net Merchant Interface, then the admin presses "Check gateway status" on the order.
/// <list type="bullet">
/// <item>Approved → payment Captured / Authorized; the order moves Payment Review → Pending.</item>
/// <item>Declined / voided → payment Failed / Voided; the order is cancelled and its stock returned.</item>
/// </list>
/// Webhooks (AN-34) would call the same logic automatically; they are optional and not built.
/// </summary>
public record SyncPaymentStatusCommand(Guid PaymentId) : IRequest<PaymentDto>;

public class SyncPaymentStatusCommandValidator : AbstractValidator<SyncPaymentStatusCommand>
{
    public SyncPaymentStatusCommandValidator() => RuleFor(x => x.PaymentId).NotEmpty();
}

public class SyncPaymentStatusCommandHandler : IRequestHandler<SyncPaymentStatusCommand, PaymentDto>
{
    /// <summary>Authorize.Net authorizations expire after 30 days (AN-23).</summary>
    public const int AuthorizeNetAuthHoldDays = 30;

    private readonly IApplicationDbContext _db;
    private readonly IPaymentGatewayRouter _router;
    private readonly IDateTimeProvider _clock;
    private readonly IInventoryService _inventory;
    private readonly ICurrentUserService? _current;
    private readonly GiftCertificates.IGiftCertificateService? _giftCertificates;

    public SyncPaymentStatusCommandHandler(
        IApplicationDbContext db, IPaymentGatewayRouter router, IDateTimeProvider clock, IInventoryService inventory,
        ICurrentUserService? current = null, GiftCertificates.IGiftCertificateService? giftCertificates = null)
    {
        _db = db;
        _router = router;
        _clock = clock;
        _inventory = inventory;
        _current = current;
        _giftCertificates = giftCertificates;
    }

    public async Task<PaymentDto> Handle(SyncPaymentStatusCommand request, CancellationToken ct)
    {
        var payment = await _db.PaymentRecords
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, ct)
            ?? throw new NotFoundException(nameof(PaymentRecord), request.PaymentId);

        var result = await _router.GetStatusAsync(payment, ct)
            ?? throw new ConflictException($"{payment.Method} does not support a gateway status lookup.");
        if (!result.Success)
            throw new ConflictException(result.ErrorMessage ?? "The gateway status could not be read.");

        var now = _clock.UtcNow;
        var from = payment.Status;
        var to = result.Status;
        // Fill gaps (card brand, last four, auth code) without replacing the sale's response text by a status word.
        if (result.Details is { } d)
            PaymentLedger.ApplyDetails(payment, result with { Details = d with { ResponseText = null } });

        // Refunds are tracked by our own refund rows; a "refunded" lookup on the original only confirms them.
        if (to == from || to == PaymentStatus.Refunded)
        {
            await _db.SaveChangesAsync(ct);
            return PaymentDto.From(payment);
        }

        PaymentLedger.Record(_db, payment, PaymentTransactionType.StatusSync, payment.Amount, result, now, _current?.UserId);
        payment.Status = to;
        payment.ErrorMessage = null;
        if (to == PaymentStatus.Captured)
        {
            payment.CapturedOnUtc ??= now;
            payment.AuthorizedOnUtc ??= now;
            payment.AuthorizationExpiresUtc = null;
        }
        else if (to == PaymentStatus.Authorized)
        {
            payment.AuthorizedOnUtc ??= now;
            payment.AuthorizationExpiresUtc ??= payment.AuthorizedOnUtc.Value.AddDays(AuthorizeNetAuthHoldDays);
        }

        var order = await _db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == payment.OrderId, ct);

        if (order is not null && payment.IsMaster)
        {
            var paidBefore = order.PaymentStatus;
            order.PaymentStatus = to;

            if (order.Status == OrderStatus.PaymentReview && to is PaymentStatus.Captured or PaymentStatus.Authorized)
            {
                AddHistory(order, OrderStatus.Pending, now, $"Payment approved at the gateway ({result.Details?.ResponseText}).");
                order.Status = OrderStatus.Pending;
            }
            else if (order.Status != OrderStatus.Cancelled && to is PaymentStatus.Failed or PaymentStatus.Voided)
            {
                AddHistory(order, OrderStatus.Cancelled, now, $"Payment {result.Details?.ResponseText ?? to.ToString()} at the gateway; order cancelled.");
                order.Status = OrderStatus.Cancelled;
                await _db.SaveChangesAsync(ct);

                // Stock was committed when the held order was placed; give it back exactly once.
                if (paidBefore is PaymentStatus.HeldForReview or PaymentStatus.Authorized or PaymentStatus.Captured)
                    await _inventory.RestoreForOrderAsync(order, ct: ct);
                if (_giftCertificates is not null)
                    await _giftCertificates.HandleOrderCancelledAsync(order.Id, ct);
            }
        }

        await _db.SaveChangesAsync(ct);

        if (to == PaymentStatus.Captured && _giftCertificates is not null)
            await _giftCertificates.IssueForOrderAsync(payment.OrderId, ct);

        return PaymentDto.From(payment);
    }

    private void AddHistory(Order order, OrderStatus to, DateTime now, string note)
        => _db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = to,
            ChangedById = _current?.UserId,
            ChangedOnUtc = now,
            Note = note,
        });
}
