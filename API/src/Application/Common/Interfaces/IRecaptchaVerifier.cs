using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Verifies a reCAPTCHA token submitted with a protected form (WO-107). Verification is skipped
/// (and succeeds) when reCAPTCHA is not configured or the form type is disabled (AC-TEN-007.6).
/// </summary>
public interface IRecaptchaVerifier
{
    Task<RecaptchaVerificationResult> VerifyAsync(RecaptchaFormType formType, string? token, CancellationToken cancellationToken = default);

    /// <summary>Verifies and throws a validation error (HTTP 400) when verification fails (AC-TEN-007.7).</summary>
    Task VerifyOrThrowAsync(RecaptchaFormType formType, string? token, CancellationToken cancellationToken = default);
}
