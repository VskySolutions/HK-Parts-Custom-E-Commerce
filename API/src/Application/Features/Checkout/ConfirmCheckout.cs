using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;

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
    private readonly IApplicationDbContext _db;
    private readonly IGuestOrderTokenService _tokens;
    private readonly ISettingsService _settings;
    private readonly ICurrentUserService _current;

    public ConfirmCheckoutCommandHandler(
        ICheckoutOrchestrator orchestrator, IApplicationDbContext db, IGuestOrderTokenService tokens,
        ISettingsService settings, ICurrentUserService current)
    {
        _orchestrator = orchestrator;
        _db = db;
        _tokens = tokens;
        _settings = settings;
        _current = current;
    }

    public async Task<CheckoutResult> Handle(ConfirmCheckoutCommand request, CancellationToken cancellationToken)
        => await (await _orchestrator.ConfirmAsync(request.OrderId, cancellationToken))
            .WithGuestTokenAsync(_db, _tokens, _settings, _current, cancellationToken);
}
