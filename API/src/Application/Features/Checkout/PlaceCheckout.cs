using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// Places (finalizes) a checkout (REQ-CHK-003). A thin MediatR wrapper that delegates to the
/// <see cref="ICheckoutOrchestrator"/>; a failed payment is returned as a non-successful
/// <see cref="CheckoutResult"/> (not an exception) so the buyer can retry (AC-PAY-001.3).
/// </summary>
public record PlaceCheckoutCommand(PlaceCheckoutRequest Request) : IRequest<CheckoutResult>;

public class PlaceCheckoutCommandHandler : IRequestHandler<PlaceCheckoutCommand, CheckoutResult>
{
    private readonly ICheckoutOrchestrator _orchestrator;
    private readonly IRecaptchaVerifier _recaptcha;

    public PlaceCheckoutCommandHandler(ICheckoutOrchestrator orchestrator, IRecaptchaVerifier recaptcha)
    {
        _orchestrator = orchestrator;
        _recaptcha = recaptcha;
    }

    public async Task<CheckoutResult> Handle(PlaceCheckoutCommand request, CancellationToken cancellationToken)
    {
        await _recaptcha.VerifyOrThrowAsync(RecaptchaFormType.GuestCheckout, request.Request.RecaptchaToken, cancellationToken);
        return await _orchestrator.PlaceAsync(request.Request, cancellationToken);
    }
}
