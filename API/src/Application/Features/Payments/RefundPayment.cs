using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>
/// Refunds a captured payment, in full or part, through its originating gateway (WO-34, REQ-PAY-003).
/// Admin-only. On success it records the cumulative <see cref="PaymentRecord.RefundedAmount"/> and moves
/// the record to <see cref="PaymentStatus.Refunded"/> / <see cref="PaymentStatus.PartiallyRefunded"/>;
/// on a gateway failure it keeps the current status and surfaces the provider error (AC-PAY-003.4).
/// </summary>
public record RefundPaymentCommand(Guid PaymentId, decimal? Amount = null, string? Reason = null) : IRequest<PaymentDto>;

/// <summary>Optional POST body for the refund endpoint (omit the amount for a full refund of the remainder).</summary>
public record RefundPaymentRequest(decimal? Amount = null, string? Reason = null);

public class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue);
    }
}

public class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGatewayRouter _router;
    private readonly IDateTimeProvider _clock;

    private readonly GiftCertificates.IGiftCertificateService? _giftCertificates;
    private readonly ICurrentUserService? _current;

    public RefundPaymentCommandHandler(
        IApplicationDbContext db, IPaymentGatewayRouter router, IDateTimeProvider clock,
        GiftCertificates.IGiftCertificateService? giftCertificates = null,
        ICurrentUserService? current = null)
    {
        _db = db;
        _router = router;
        _clock = clock;
        _giftCertificates = giftCertificates;
        _current = current;
    }

    public async Task<PaymentDto> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _db.PaymentRecords
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentRecord), request.PaymentId);

        if (payment.Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded))
            throw new ConflictException($"A payment in status '{payment.Status}' cannot be refunded.");

        var remaining = payment.Amount - payment.RefundedAmount;
        var amount = request.Amount ?? remaining;

        if (amount <= 0 || amount > remaining)
            throw new ConflictException(
                $"The refund amount must be greater than 0 and at most the remaining {remaining:0.00} {payment.CurrencyCode}.");

        // A gift-certificate payment is refunded back onto the certificate (ledger "Refunded"), never via a gateway.
        if (payment.Method == PaymentMethodType.GiftCertificate)
        {
            if (_giftCertificates is null)
                throw new ConflictException("Gift certificate refunds are not available.");
            await _giftCertificates.RestoreForRefundAsync(payment, amount, request.Reason, cancellationToken);
            return PaymentDto.From(payment);
        }

        var result = await _router.RefundAsync(payment, amount, cancellationToken);
        PaymentLedger.Record(_db, payment, PaymentTransactionType.Refund, amount, result, _clock.UtcNow, _current?.UserId);

        if (result.Success)
        {
            payment.RefundedAmount += amount;
            payment.RefundedOnUtc = _clock.UtcNow;
            // A same-day full refund the gateway ran as a void (Authorize.Net, unsettled): nothing settled at all.
            payment.Status = result.Status == PaymentStatus.Voided
                ? PaymentStatus.Voided
                : payment.RefundedAmount >= payment.Amount
                    ? PaymentStatus.Refunded
                    : PaymentStatus.PartiallyRefunded;
            // TransactionId stays the ORIGINAL transaction: every further partial refund references it (AN-25).
            // The refund's own id lives on its PaymentTransaction row.
            payment.ErrorMessage = null;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, cancellationToken);
            if (order is not null)
                order.PaymentStatus = payment.Status;

            // A fully refunded purchase voids the gift certificates it bought (unused ones; partly used flags review).
            if (order is not null && payment.Status is PaymentStatus.Refunded or PaymentStatus.Voided && _giftCertificates is not null)
            {
                await _db.SaveChangesAsync(cancellationToken);
                await _giftCertificates.VoidPurchasedAsync(order.Id, "refunded", cancellationToken);
            }
        }
        else
        {
            // Keep the captured state intact; surface the gateway error (AC-PAY-003.4).
            payment.ErrorMessage = result.ErrorMessage;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // AN-27: a failed refund must not look like a success. Throwing (after the failure row is saved) stops
        // RefundOrder from restocking / emailing the customer, and the admin sees the gateway's reason.
        if (!result.Success)
            throw new ConflictException(result.ErrorMessage ?? "The gateway did not accept the refund.");

        return PaymentDto.From(payment);
    }
}
