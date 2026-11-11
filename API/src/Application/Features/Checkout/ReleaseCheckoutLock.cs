using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>Releases the cart's low-stock checkout locks when the storefront countdown runs out (CK-23).</summary>
public record ReleaseCheckoutLockCommand(CheckoutCartRef Cart) : IRequest;

public class ReleaseCheckoutLockCommandHandler : IRequestHandler<ReleaseCheckoutLockCommand>
{
    private readonly ICheckoutOrchestrator _orchestrator;

    public ReleaseCheckoutLockCommandHandler(ICheckoutOrchestrator orchestrator) => _orchestrator = orchestrator;

    public Task Handle(ReleaseCheckoutLockCommand request, CancellationToken cancellationToken)
        => _orchestrator.ReleaseCheckoutLockAsync(request.Cart, cancellationToken);
}
