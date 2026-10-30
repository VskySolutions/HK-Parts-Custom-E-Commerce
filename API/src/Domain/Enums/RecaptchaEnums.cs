namespace HKPartsEcommerce.Domain.Enums;

/// <summary>reCAPTCHA integration variant (REQ-TEN-007, AC-TEN-007.2).</summary>
public enum RecaptchaVersion
{
    V2Checkbox = 0,
    V2Invisible = 1,
    V3 = 2
}

/// <summary>Behaviour when the Google verification API is unreachable (AC-TEN-007.8).</summary>
public enum RecaptchaFailBehaviour
{
    FailOpen = 0,
    FailClosed = 1
}

/// <summary>The eight protected form types a reCAPTCHA challenge can be enabled for (AC-TEN-007.4).</summary>
public enum RecaptchaFormType
{
    Register = 0,
    Login = 1,
    PasswordReset = 2,
    GuestCheckout = 3,
    Contact = 4,
    Newsletter = 5,
    ReviewSubmit = 6,
    QaSubmit = 7,

    /// <summary>The storefront inquiry (quote-request) form — a guest-facing lead form, so spam-prone.</summary>
    Inquiry = 8
}
