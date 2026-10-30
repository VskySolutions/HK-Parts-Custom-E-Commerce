namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>Outcome of a server-side reCAPTCHA verification (WO-107). A skipped (disabled/unconfigured) check succeeds.</summary>
public record RecaptchaVerificationResult(bool Success, string? Error = null, double? Score = null)
{
    public static RecaptchaVerificationResult Ok(double? score = null) => new(true, null, score);
    public static RecaptchaVerificationResult Fail(string error) => new(false, error);
}
