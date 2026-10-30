using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// Re-opens a payment session for a still-pending order after a cancelled/failed redirect payment
/// (Stripe Checkout). Returns a fresh <see cref="CheckoutResult.RedirectUrl"/> to send the buyer back to.
/// </summary>
public record RetryCheckoutPaymentCommand(Guid OrderId) : IRequest<CheckoutResult>;

public class RetryCheckoutPaymentCommandHandler : IRequestHandler<RetryCheckoutPaymentCommand, CheckoutResult>
{
    private readonly ICheckoutOrchestrator _orchestrator;

    public RetryCheckoutPaymentCommandHandler(ICheckoutOrchestrator orchestrator) => _orchestrator = orchestrator;

    public Task<CheckoutResult> Handle(RetryCheckoutPaymentCommand request, CancellationToken cancellationToken)
        => _orchestrator.RetryPaymentAsync(request.OrderId, cancellationToken);
}
