using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Singleton Google reCAPTCHA configuration for the tenant (REQ-TEN-007). The Secret Key is stored
/// encrypted (via the Credential Vault) and never returned to clients; the Site Key is public.
/// All per-form protection flags default to disabled (AC-TEN-007.4/007.6).
/// </summary>
public class RecaptchaConfig : AuditableEntity
{
    public string? SiteKey { get; set; }

    /// <summary>Secret Key ciphertext (encrypted via <see cref="Application.Common.Interfaces.ICredentialVault"/>).</summary>
    public string? SecretKeyEncrypted { get; set; }
    /// <summary>Last four characters of the Secret Key for masked display (AC-TEN-007.1).</summary>
    public string? SecretKeyLast4 { get; set; }

    public RecaptchaVersion Version { get; set; } = RecaptchaVersion.V3;

    /// <summary>v3 minimum passing score, 0.0–1.0 (AC-TEN-007.3).</summary>
    public decimal ScoreThreshold { get; set; } = 0.5m;

    public RecaptchaFailBehaviour FailBehaviour { get; set; } = RecaptchaFailBehaviour.FailOpen;

    // Per-form enable flags — all default false (AC-TEN-007.4).
    public bool ProtectRegister { get; set; }
    public bool ProtectLogin { get; set; }
    public bool ProtectPasswordReset { get; set; }
    public bool ProtectGuestCheckout { get; set; }
    public bool ProtectContact { get; set; }
    public bool ProtectNewsletter { get; set; }
    public bool ProtectReviewSubmit { get; set; }
    public bool ProtectQaSubmit { get; set; }

    /// <summary>Protect the storefront inquiry (quote-request) form.</summary>
    public bool ProtectInquiry { get; set; }

    /// <summary>True once a Site Key and Secret Key are present; forms skip verification otherwise (AC-TEN-007.6).</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKeyEncrypted);

    /// <summary>Returns whether the given form type is protected.</summary>
    public bool IsFormProtected(RecaptchaFormType form) => form switch
    {
        RecaptchaFormType.Register => ProtectRegister,
        RecaptchaFormType.Login => ProtectLogin,
        RecaptchaFormType.PasswordReset => ProtectPasswordReset,
        RecaptchaFormType.GuestCheckout => ProtectGuestCheckout,
        RecaptchaFormType.Contact => ProtectContact,
        RecaptchaFormType.Newsletter => ProtectNewsletter,
        RecaptchaFormType.ReviewSubmit => ProtectReviewSubmit,
        RecaptchaFormType.QaSubmit => ProtectQaSubmit,
        RecaptchaFormType.Inquiry => ProtectInquiry,
        _ => false
    };
}
