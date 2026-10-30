using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// A gateway-agnostic request to authorize a payment against an order (REQ-PAY-001). The
/// <see cref="PaymentGatewayRouter"/> resolves the concrete gateway from <see cref="Method"/>; the
/// optional <see cref="PaymentToken"/> carries a client-side nonce/approval id where the provider
/// requires one (Stripe payment method, PayPal approved-order id, Square nonce, etc.).
/// </summary>
public record PaymentRequest(
    Guid OrderId,
    PaymentMethodType Method,
    decimal Amount,
    string CurrencyCode,
    string? PaymentToken = null,
    string? ReturnUrl = null,
    IDictionary<string, string>? Metadata = null,
    string? OrderNumber = null);

/// <summary>
/// A payment method currently offered at checkout: the <see cref="Method"/> plus its environment —
/// <see cref="IsProduction"/> is <c>true</c> for a live credential, <c>false</c> for sandbox/test, and
/// <c>null</c> for manual methods (Cash on Delivery, Bank Transfer) that have no gateway environment.
/// <see cref="FeePercent"/> is the gateway's configured transaction fee (% of order total), 0 when none.
/// </summary>
public record PaymentMethodAvailability(PaymentMethodType Method, bool? IsProduction, decimal FeePercent = 0m);

/// <summary>
/// The normalized outcome of an authorize/capture/refund call, mapped from each provider's native
/// response so callers never depend on gateway-specific shapes (REQ-PAY-001/002/003).
/// </summary>
public record PaymentResult(
    bool Success,
    PaymentStatus Status,
    string? AuthorizationId = null,
    string? TransactionId = null,
    string? GatewayReference = null,
    string? ErrorMessage = null,
    decimal? CapturedAmount = null)
{
    /// <summary>
    /// For redirect gateways (e.g. Stripe Checkout): the URL to send the buyer to complete payment off-site.
    /// Null for inline gateways. When set, the checkout returns this URL instead of finalizing the order.
    /// </summary>
    public string? RedirectUrl { get; init; }

    /// <summary>
    /// For client-completed gateways (e.g. Razorpay Checkout): the provider-specific fields the storefront
    /// needs to open the on-site payment widget — e.g. { keyId } alongside the provider order id in
    /// <see cref="GatewayReference"/>. Null for inline/redirect gateways. When set, the checkout returns this
    /// to the client instead of finalizing; the buyer pays in the widget and the tokens it returns are
    /// verified via <see cref="Interfaces.IPaymentGatewayRouter.VerifyClientPaymentAsync"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ClientAction { get; init; }

    /// <summary>A "send the buyer to this URL" result for redirect gateways; the order stays pending until they return.</summary>
    public static PaymentResult Redirect(string url, string? gatewayReference)
        => new(false, PaymentStatus.Pending, GatewayReference: gatewayReference) { RedirectUrl = url };

    /// <summary>
    /// A "the buyer must complete payment in an on-site widget" result (Razorpay Checkout). The order stays
    /// pending until the widget completes and its tokens are verified. <paramref name="gatewayReference"/>
    /// is the provider order id; <paramref name="action"/> carries the widget config (e.g. the public key).
    /// </summary>
    public static PaymentResult ClientActionRequired(string gatewayReference, IReadOnlyDictionary<string, string> action)
        => new(false, PaymentStatus.Pending, GatewayReference: gatewayReference) { ClientAction = action };

    /// <summary>A failed result (default status <see cref="PaymentStatus.Failed"/>) carrying the gateway error.</summary>
    public static PaymentResult Failed(string error, PaymentStatus status = PaymentStatus.Failed)
        => new(false, status, ErrorMessage: error);

    /// <summary>A successful authorization-only hold (funds reserved, not yet captured).</summary>
    public static PaymentResult Authorized(string? authorizationId, string? gatewayReference = null)
        => new(true, PaymentStatus.Authorized, authorizationId, GatewayReference: gatewayReference ?? authorizationId);

    /// <summary>A successful capture (funds settled).</summary>
    public static PaymentResult Captured(string? transactionId, decimal capturedAmount, string? authorizationId = null, string? gatewayReference = null)
        => new(true, PaymentStatus.Captured, authorizationId, transactionId, gatewayReference ?? transactionId, CapturedAmount: capturedAmount);
}
