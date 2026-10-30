using System.Text.Json;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>
/// Calls Google's reCAPTCHA verification API to validate submitted tokens (WO-107). Version-aware
/// (v3 score threshold), applies the configured fail behaviour when Google is unreachable, and skips
/// verification when reCAPTCHA is unconfigured or the form is disabled.
/// </summary>
public class RecaptchaVerifier : IRecaptchaVerifier
{
    private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    private readonly IApplicationDbContext _db;
    private readonly ICredentialVault _vault;
    private readonly IHttpClientFactory _httpFactory;

    public RecaptchaVerifier(IApplicationDbContext db, ICredentialVault vault, IHttpClientFactory httpFactory)
    {
        _db = db;
        _vault = vault;
        _httpFactory = httpFactory;
    }

    public async Task<RecaptchaVerificationResult> VerifyAsync(RecaptchaFormType formType, string? token, CancellationToken cancellationToken = default)
    {
        var config = await _db.RecaptchaConfigs.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        // Not configured, or this form isn't protected → skip verification and allow (AC-TEN-007.6).
        if (config is null || !config.IsConfigured || !config.IsFormProtected(formType))
            return RecaptchaVerificationResult.Ok();

        if (string.IsNullOrWhiteSpace(token))
            return RecaptchaVerificationResult.Fail("reCAPTCHA response is missing. Please complete the verification and try again.");

        var secret = string.IsNullOrWhiteSpace(config.SecretKeyEncrypted) ? null : _vault.Decrypt(config.SecretKeyEncrypted);
        if (string.IsNullOrWhiteSpace(secret))
            return RecaptchaVerificationResult.Ok();

        try
        {
            var client = _httpFactory.CreateClient();
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = secret,
                ["response"] = token!,
            });
            var response = await client.PostAsync(VerifyUrl, content, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            double? score = root.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number
                ? sc.GetDouble()
                : null;

            if (!success)
                return RecaptchaVerificationResult.Fail("reCAPTCHA verification failed. Please try again.");

            // v3: reject when the risk score is below the configured threshold (AC-TEN-007.3).
            if (config.Version == RecaptchaVersion.V3 && score.HasValue && score.Value < (double)config.ScoreThreshold)
                return RecaptchaVerificationResult.Fail("reCAPTCHA verification failed. Please try again.");

            return RecaptchaVerificationResult.Ok(score);
        }
        catch (Exception)
        {
            // Google unreachable → apply the configured fail behaviour (AC-TEN-007.8; default fail-open).
            return config.FailBehaviour == RecaptchaFailBehaviour.FailClosed
                ? RecaptchaVerificationResult.Fail("reCAPTCHA verification is temporarily unavailable. Please try again.")
                : RecaptchaVerificationResult.Ok();
        }
    }

    public async Task VerifyOrThrowAsync(RecaptchaFormType formType, string? token, CancellationToken cancellationToken = default)
    {
        var result = await VerifyAsync(formType, token, cancellationToken);
        if (!result.Success)
            throw new ValidationException(new[] { new ValidationFailure("recaptcha", result.Error ?? "reCAPTCHA verification failed.") });
    }
}
