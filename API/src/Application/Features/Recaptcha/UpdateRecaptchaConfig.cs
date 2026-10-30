using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Recaptcha;

/// <summary>
/// Creates or updates the singleton reCAPTCHA config. The Secret Key is encrypted via the Credential
/// Vault; a null/blank <see cref="SecretKey"/> keeps the stored value unchanged (AC-TEN-007.1).
/// </summary>
public record UpdateRecaptchaConfigCommand(
    string? SiteKey,
    string? SecretKey,
    RecaptchaVersion Version,
    decimal ScoreThreshold,
    RecaptchaFailBehaviour FailBehaviour,
    RecaptchaFormSettings? PerFormSettings) : IRequest<RecaptchaConfigDto>;

public class UpdateRecaptchaConfigCommandValidator : AbstractValidator<UpdateRecaptchaConfigCommand>
{
    public UpdateRecaptchaConfigCommandValidator()
    {
        RuleFor(x => x.SiteKey).MaximumLength(200);
        RuleFor(x => x.ScoreThreshold).InclusiveBetween(0m, 1m);
    }
}

public class UpdateRecaptchaConfigCommandHandler : IRequestHandler<UpdateRecaptchaConfigCommand, RecaptchaConfigDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICredentialVault _vault;

    public UpdateRecaptchaConfigCommandHandler(IApplicationDbContext db, ICredentialVault vault)
    {
        _db = db;
        _vault = vault;
    }

    public async Task<RecaptchaConfigDto> Handle(UpdateRecaptchaConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await _db.RecaptchaConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config is null)
        {
            config = new RecaptchaConfig();
            _db.RecaptchaConfigs.Add(config);
        }

        config.SiteKey = request.SiteKey?.Trim();
        config.Version = request.Version;
        config.ScoreThreshold = request.ScoreThreshold;
        config.FailBehaviour = request.FailBehaviour;

        var forms = request.PerFormSettings ?? new RecaptchaFormSettings();
        config.ProtectRegister = forms.Register;
        config.ProtectLogin = forms.Login;
        config.ProtectPasswordReset = forms.PasswordReset;
        config.ProtectGuestCheckout = forms.GuestCheckout;
        config.ProtectContact = forms.Contact;
        config.ProtectNewsletter = forms.Newsletter;
        config.ProtectReviewSubmit = forms.ReviewSubmit;
        config.ProtectQaSubmit = forms.QaSubmit;
        config.ProtectInquiry = forms.Inquiry;

        // Only replace the secret when a new value is supplied; blank keeps the stored ciphertext.
        if (!string.IsNullOrWhiteSpace(request.SecretKey))
        {
            var secret = request.SecretKey.Trim();
            config.SecretKeyEncrypted = _vault.Encrypt(secret);
            config.SecretKeyLast4 = secret.Length >= 4 ? secret[^4..] : secret;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return RecaptchaConfigDto.From(config);
    }
}
