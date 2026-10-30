using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// Request body for confirming an on-site widget payment: the order id plus the raw tokens the widget
/// returned (e.g. Razorpay's <c>razorpay_payment_id</c> / <c>razorpay_order_id</c> / <c>razorpay_signature</c>).
/// The gateway data is passed through opaquely so each provider reads the fields it needs.
/// </summary>
public record ConfirmClientPaymentRequest(Guid OrderId, Dictionary<string, string>? GatewayData);

/// <summary>
/// Confirms an on-site widget payment (Razorpay Checkout): verifies the tokens the widget returned and,
/// when the payment checks out, captures it, consumes the cart, and finalizes the order. Idempotent —
/// re-confirming an already-paid order just returns success. Delegates to <see cref="ICheckoutOrchestrator"/>.
/// </summary>
public record ConfirmClientPaymentCommand(Guid OrderId, IReadOnlyDictionary<string, string> GatewayData)
    : IRequest<CheckoutResult>;

public class ConfirmClientPaymentCommandHandler : IRequestHandler<ConfirmClientPaymentCommand, CheckoutResult>
{
    private readonly ICheckoutOrchestrator _orchestrator;

    public ConfirmClientPaymentCommandHandler(ICheckoutOrchestrator orchestrator) => _orchestrator = orchestrator;

    public Task<CheckoutResult> Handle(ConfirmClientPaymentCommand request, CancellationToken cancellationToken)
        => _orchestrator.ConfirmClientPaymentAsync(request.OrderId, request.GatewayData, cancellationToken);
}
