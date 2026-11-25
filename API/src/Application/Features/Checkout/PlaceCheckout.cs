using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// Places (finalizes) a checkout (REQ-CHK-003). A thin MediatR wrapper that delegates to the
/// <see cref="ICheckoutOrchestrator"/>; a failed payment is returned as a non-successful
/// <see cref="CheckoutResult"/> (not an exception) so the buyer can retry (AC-PAY-001.3).
/// A successful guest order also carries a signed <see cref="CheckoutResult.GuestOrderToken"/> (U-53).
/// </summary>
public record PlaceCheckoutCommand(PlaceCheckoutRequest Request) : IRequest<CheckoutResult>;

/// <summary>
/// Guest checkout U-10/U-11 (D4): the order contact email is required and must be a real address of at most
/// 254 characters. It is stored trimmed and lower-case by the orchestrator.
/// </summary>
public class PlaceCheckoutCommandValidator : AbstractValidator<PlaceCheckoutCommand>
{
    public PlaceCheckoutCommandValidator()
    {
        RuleFor(x => x.Request.ShipTo).NotNull();
        RuleFor(x => x.Request.ShipTo.Email)
            .Cascade(CascadeMode.Stop)
            .Must(e => !string.IsNullOrWhiteSpace(e)).WithMessage("Email is required.")
            .Must(e => e!.Trim().Length <= 254).WithMessage("Email must be 254 characters or fewer.")
            .Must(e => GuestEmail.IsValid(e)).WithMessage("Enter a valid email address.")
            .When(x => x.Request.ShipTo is not null)
            .OverridePropertyName("Email");
    }
}

/// <summary>Email format rule shared by checkout and guest order lookup.</summary>
public static class GuestEmail
{
    /// <summary>One "@", a non-empty local part, and a dotted domain with no spaces.</summary>
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        var e = email.Trim();
        var at = e.IndexOf('@');
        if (at <= 0 || at != e.LastIndexOf('@') || e.Any(char.IsWhiteSpace))
            return false;
        var domain = e[(at + 1)..];
        return domain.Length >= 3 && domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.') && !domain.Contains("..");
    }
}

public class PlaceCheckoutCommandHandler : IRequestHandler<PlaceCheckoutCommand, CheckoutResult>
{
    private readonly ICheckoutOrchestrator _orchestrator;
    private readonly IRecaptchaVerifier _recaptcha;
    private readonly IApplicationDbContext _db;
    private readonly IGuestOrderTokenService _tokens;
    private readonly ISettingsService _settings;
    private readonly ICurrentUserService _current;
    private readonly IEmailDomainChecker? _domains;

    public PlaceCheckoutCommandHandler(
        ICheckoutOrchestrator orchestrator, IRecaptchaVerifier recaptcha, IApplicationDbContext db,
        IGuestOrderTokenService tokens, ISettingsService settings, ICurrentUserService current,
        IEmailDomainChecker? domains = null)
    {
        _orchestrator = orchestrator;
        _recaptcha = recaptcha;
        _db = db;
        _tokens = tokens;
        _settings = settings;
        _current = current;
        _domains = domains;
    }

    public async Task<CheckoutResult> Handle(PlaceCheckoutCommand request, CancellationToken cancellationToken)
    {
        await _recaptcha.VerifyOrThrowAsync(RecaptchaFormType.GuestCheckout, request.Request.RecaptchaToken, cancellationToken);

        // U-11: a guest's receipt must be deliverable, so the email's domain must take mail (MX, or an address).
        if (_current.UserId is null && _domains is not null
            && !await _domains.CanReceiveMailAsync(request.Request.ShipTo.Email, cancellationToken))
            throw new Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("Email",
                    "This email address can't receive mail. Please check it, so we can send your receipt."),
            });

        var result = await _orchestrator.PlaceAsync(request.Request, cancellationToken);
        return await result.WithGuestTokenAsync(_db, _tokens, _settings, _current, cancellationToken);
    }
}
