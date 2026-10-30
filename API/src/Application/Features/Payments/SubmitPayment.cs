using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>
/// Submits a payment for an order (WO-32, REQ-PAY-001). Creates or reuses a <see cref="PaymentRecord"/>,
/// authorizes it through the <see cref="IPaymentGatewayRouter"/> (which applies the gateway's capture
/// mode) and persists the normalized outcome. Public checkout entry point — anonymous.
/// </summary>
public record SubmitPaymentCommand(
    Guid OrderId,
    PaymentMethodType Method,
    decimal? Amount = null,
    string? CurrencyCode = null,
    string? PaymentToken = null,
    string? ReturnUrl = null) : IRequest<PaymentDto>;

public class SubmitPaymentCommandValidator : AbstractValidator<SubmitPaymentCommand>
{
    public SubmitPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue);
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
    }
}

public class SubmitPaymentCommandHandler : IRequestHandler<SubmitPaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGatewayRouter _router;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public SubmitPaymentCommandHandler(
        IApplicationDbContext db,
        IPaymentGatewayRouter router,
        ISettingsService settings,
        IDateTimeProvider clock)
    {
        _db = db;
        _router = router;
        _settings = settings;
        _clock = clock;
    }

    public async Task<PaymentDto> Handle(SubmitPaymentCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        var amount = request.Amount ?? order.TotalAmount;
        var currency = string.IsNullOrWhiteSpace(request.CurrencyCode) ? order.CurrencyCode : request.CurrencyCode!;

        if (amount <= 0)
            throw new ConflictException("The order has no payable amount.");

        // Reuse a still-open record for this method (retry after failure) or start a fresh attempt.
        var payment = await _db.PaymentRecords.FirstOrDefaultAsync(
            p => p.OrderId == order.Id && p.Method == request.Method
                 && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Failed),
            cancellationToken);

        if (payment is null)
        {
            payment = new PaymentRecord { OrderId = order.Id, Method = request.Method };
            _db.PaymentRecords.Add(payment);
        }

        payment.Amount = amount;
        payment.CurrencyCode = currency;
        payment.GatewayName = request.Method.ToString();
        payment.Status = PaymentStatus.Pending;
        payment.ErrorMessage = null;

        var result = await _router.AuthorizeAsync(
            new PaymentRequest(order.Id, request.Method, amount, currency, request.PaymentToken, request.ReturnUrl),
            cancellationToken);

        await ApplyResultAsync(payment, order, result, request.Method, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return PaymentDto.From(payment);
    }

    private async Task ApplyResultAsync(
        PaymentRecord payment, Order order, PaymentResult result, PaymentMethodType method, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        payment.Status = result.Status;
        payment.AuthorizationId = result.AuthorizationId ?? payment.AuthorizationId;
        payment.TransactionId = result.TransactionId ?? payment.TransactionId;
        payment.GatewayReference = result.GatewayReference ?? payment.GatewayReference;
        payment.ErrorMessage = result.ErrorMessage;

        switch (result.Status)
        {
            case PaymentStatus.Captured:
                payment.AuthorizedOnUtc ??= now;
                payment.CapturedOnUtc = now;
                payment.AuthorizationExpiresUtc = null;
                break;

            case PaymentStatus.Authorized:
                payment.AuthorizedOnUtc = now;
                // Record when the uncaptured hold lapses, so a scan can flag it for review (AC-PAY-002.4).
                payment.AuthorizationExpiresUtc = now.AddDays(await GetAuthHoldDaysAsync(method, ct));
                break;
        }

        // Mirror the payment state onto the order for at-a-glance status.
        order.PaymentStatus = result.Status;
    }

    private async Task<int> GetAuthHoldDaysAsync(PaymentMethodType method, CancellationToken ct)
    {
        var raw = await _settings.GetValueAsync(PaymentGatewayDefaultsKeys.AuthHoldDaysKey(method), ct);
        return int.TryParse(raw, out var days) && days > 0 ? days : PaymentGatewayDefaultsKeys.DefaultAuthHoldDays;
    }
}

/// <summary>
/// Settings-key builders mirrored in the Application layer (the Infrastructure equivalent is internal).
/// Kept tiny and local so handlers can read per-gateway settings without an Infrastructure dependency.
/// </summary>
internal static class PaymentGatewayDefaultsKeys
{
    public const int DefaultAuthHoldDays = 7;
    public static string AuthHoldDaysKey(PaymentMethodType method) => $"payments:{method}:authHoldDays";
}
