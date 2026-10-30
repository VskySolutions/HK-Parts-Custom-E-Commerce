using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>Request body carrying just the order id — used by both confirm and retry-payment.</summary>
public record CheckoutOrderRef(Guid OrderId);

/// <summary>
/// Confirms a redirect payment on the buyer's return (Stripe Checkout): verifies the gateway session and,
/// when paid, captures the payment, consumes the cart, and finalizes the order. Idempotent — re-confirming
/// an already-paid order just returns success. Delegates to <see cref="ICheckoutOrchestrator"/>.
/// </summary>
public record ConfirmCheckoutCommand(Guid OrderId) : IRequest<CheckoutResult>;

public class ConfirmCheckoutCommandHandler : IRequestHandler<ConfirmCheckoutCommand, CheckoutResult>
{
    private readonly ICheckoutOrchestrator _orchestrator;

    public ConfirmCheckoutCommandHandler(ICheckoutOrchestrator orchestrator) => _orchestrator = orchestrator;

    public Task<CheckoutResult> Handle(ConfirmCheckoutCommand request, CancellationToken cancellationToken)
        => _orchestrator.ConfirmAsync(request.OrderId, cancellationToken);
}
