using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>
/// Captures a previously authorized payment, in full or part (WO-34, AC-PAY-002). Admin-only. A gateway
/// failure keeps the record's status unchanged and surfaces the provider error on the returned record.
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

    public CapturePaymentCommandHandler(IApplicationDbContext db, IPaymentGatewayRouter router, IDateTimeProvider clock)
    {
        _db = db;
        _router = router;
        _clock = clock;
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

        var amount = request.Amount ?? payment.Amount;
        if (amount > payment.Amount)
            throw new ConflictException("The capture amount exceeds the authorized amount.");

        var result = await _router.CaptureAsync(payment, amount, cancellationToken);

        if (result.Success)
        {
            payment.Status = PaymentStatus.Captured;
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
        return PaymentDto.From(payment);
    }
}
