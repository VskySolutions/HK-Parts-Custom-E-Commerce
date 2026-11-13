using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>
/// Captures a previously authorized payment, in full or part (WO-34, AC-PAY-002; Authorize.Net "Charge",
/// AN-20/21). Admin-only. Never more than the authorized amount. Every attempt is written to the payment's
/// transaction history; a gateway failure keeps the authorization, records the error and returns 409.
/// </summary>
public record CapturePaymentCommand(Guid PaymentId, decimal? Amount = null) : IRequest<PaymentDto>;

/// <summary>Optional POST body for the capture endpoint (omit for a full capture).</summary>
public record CapturePaymentRequest(decimal? Amount = null);

public class CapturePaymentCommandValidator : AbstractValidator<CapturePaymentCommand>
{
    public CapturePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue);
    }
}

public class CapturePaymentCommandHandler : IRequestHandler<CapturePaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGatewayRouter _router;
    private readonly IDateTimeProvider _clock;

    private readonly GiftCertificates.IGiftCertificateService? _giftCertificates;
    private readonly ICurrentUserService? _current;

    public CapturePaymentCommandHandler(
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

    public async Task<PaymentDto> Handle(CapturePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _db.PaymentRecords
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentRecord), request.PaymentId);

        // Gateway auths sit in Authorized; manual methods (COD/Bank Transfer) sit in AwaitingPayment
        // until the store/admin confirms funds received (AC-PAY-001.5).
        if (payment.Status is not (PaymentStatus.Authorized or PaymentStatus.AwaitingPayment))
            throw new ConflictException($"A payment in status '{payment.Status}' cannot be captured.");

        // AN-21: the ceiling is what the gateway authorized (after any gift certificate), never the order total.
        var authorized = payment.AuthorizedAmount ?? payment.Amount;
        var amount = request.Amount ?? payment.Amount;
        if (amount > authorized)
            throw new ConflictException(
                $"The capture amount exceeds the authorized amount of {authorized:0.00} {payment.CurrencyCode}.");

        var result = await _router.CaptureAsync(payment, amount, cancellationToken);
        PaymentLedger.Record(_db, payment, PaymentTransactionType.Capture, amount, result, _clock.UtcNow, _current?.UserId);

        if (result.Success)
        {
            payment.Status = PaymentStatus.Captured;
            payment.AuthorizedAmount ??= payment.Amount;
            // A lower capture (edited total) settles only that much: refunds are bounded by it from now on.
            payment.Amount = amount;
            payment.TransactionId = result.TransactionId ?? payment.TransactionId;
            payment.GatewayReference = result.GatewayReference ?? payment.GatewayReference;
            payment.CapturedOnUtc = _clock.UtcNow;
            payment.AuthorizationExpiresUtc = null;
            payment.ErrorMessage = null;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, cancellationToken);
            if (order is not null)
                order.PaymentStatus = PaymentStatus.Captured;
        }
        else
        {
            // Keep the authorization; surface the gateway error for the admin to act on.
            payment.ErrorMessage = result.ErrorMessage;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // AN-27 style: a failed capture is an error the admin must see, not a 200 with a quiet message.
        if (!result.Success)
            throw new ConflictException(result.ErrorMessage ?? "The gateway did not capture the payment.");

        // Marked paid (invoice / PO / COD captured): issue the gift certificates the order bought (idempotent).
        if (result.Success && _giftCertificates is not null)
            await _giftCertificates.IssueForOrderAsync(payment.OrderId, cancellationToken);
        return PaymentDto.From(payment);
    }
}
