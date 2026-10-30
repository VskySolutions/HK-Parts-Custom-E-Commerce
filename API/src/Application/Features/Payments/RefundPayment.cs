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

    public RefundPaymentCommandHandler(IApplicationDbContext db, IPaymentGatewayRouter router, IDateTimeProvider clock)
    {
        _db = db;
        _router = router;
        _clock = clock;
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

        var result = await _router.RefundAsync(payment, amount, cancellationToken);

        if (result.Success)
        {
            payment.RefundedAmount += amount;
            payment.RefundedOnUtc = _clock.UtcNow;
            payment.Status = payment.RefundedAmount >= payment.Amount
                ? PaymentStatus.Refunded
                : PaymentStatus.PartiallyRefunded;
            payment.GatewayReference = result.GatewayReference ?? payment.GatewayReference;
            payment.TransactionId = result.TransactionId ?? payment.TransactionId;
            payment.ErrorMessage = null;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, cancellationToken);
            if (order is not null)
                order.PaymentStatus = payment.Status;
        }
        else
        {
            // Keep the captured state intact; surface the gateway error (AC-PAY-003.4).
            payment.ErrorMessage = result.ErrorMessage;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return PaymentDto.From(payment);
    }
}
