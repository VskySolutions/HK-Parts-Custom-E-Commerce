using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments;

/// <summary>
/// Central mapping between a <see cref="PaymentMethodType"/> and its Credential Vault service type +
/// platform-settings keys. Shared by the router and the adapters so gateway naming never drifts. The
/// vault service-type strings match the ones probed by the credential connectivity checker (WO-7).
/// </summary>
internal static class PaymentGatewayDefaults
{
    /// <summary>The Credential Vault service type for a gateway, or <c>null</c> for manual methods.</summary>
    public static string? CredentialServiceType(PaymentMethodType method) => method switch
    {
        PaymentMethodType.Stripe => "stripe",
        PaymentMethodType.PayPal => "paypal",
        PaymentMethodType.Razorpay => "razorpay",
        PaymentMethodType.Square => "square",
        PaymentMethodType.AuthorizeNet => "authorizenet",
        _ => null, // CashOnDelivery / BankTransfer are manual — no external credentials.
    };

    /// <summary>Manual methods settle out-of-band (no gateway call): Cash on Delivery, Bank Transfer.</summary>
    public static bool IsManual(PaymentMethodType method)
        => method is PaymentMethodType.CashOnDelivery or PaymentMethodType.BankTransfer;

    /// <summary>Settings key for a gateway's capture mode, e.g. <c>payments:Stripe:captureMode</c>.</summary>
    public static string CaptureModeKey(PaymentMethodType method) => $"payments:{method}:captureMode";

    /// <summary>Settings key toggling whether a method is offered at checkout, e.g. <c>payments:PayPal:enabled</c>.</summary>
    public static string EnabledKey(PaymentMethodType method) => $"payments:{method}:enabled";

    /// <summary>Settings key for the authorization-hold window (days) before an uncaptured auth expires.</summary>
    public static string AuthHoldDaysKey(PaymentMethodType method) => $"payments:{method}:authHoldDays";

    /// <summary>Default authorization-hold window when none is configured (AC-PAY-002.4).</summary>
    public const int DefaultAuthHoldDays = 7;
}
