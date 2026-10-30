using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// A ship-to (and contact) address supplied at checkout (REQ-CHK-003). Carries the buyer's name/email
/// for the order contact, the postal address used for routing, shipping and tax, and optional
/// geo-coordinates that improve nearest-store routing and carrier rating.
/// </summary>
public record CheckoutAddress(
    string FirstName,
    string LastName,
    string Email,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string? PostalCode,
    string CountryCode,
    double? Latitude = null,
    double? Longitude = null,
    string? Landmark = null,
    string? PhoneNumber = null);

/// <summary>
/// Input to a checkout price quote (REQ-CHK-003): the cart to price (by <see cref="CartId"/> or guest
/// <see cref="SessionId"/>, or the current customer's active cart when both are null), the delivery
/// <see cref="ShipTo"/> address, and an optional preferred <see cref="ShippingMethodId"/>.
/// </summary>
public record CheckoutQuoteRequest(
    Guid? CartId,
    string? SessionId,
    CheckoutAddress ShipTo,
    string? ShippingMethodId,
    bool PickupInStore = false,
    Guid? PickupStoreId = null);

/// <summary>
/// The priced outcome of a checkout quote: the cart subtotal, itemized discounts, the available
/// shipping options and the selected shipping cost, the tax breakdown, and the grand total — plus the
/// routing outcome (<see cref="AssignedStoreId"/>/<see cref="IsRoutable"/>) and whether guest ordering
/// is permitted for the assigned store (<see cref="GuestOrderingAllowed"/>, AC-CHK-003.2).
/// </summary>
public class CheckoutQuote
{
    public decimal Subtotal { get; set; }

    /// <summary>
    /// The cart subtotal BEFORE Customer Group pricing — the lines at their list price. Equals
    /// <see cref="Subtotal"/> when the buyer is in no discounting group. Surfaced so the summary can show
    /// this as "Subtotal" and itemize <see cref="GroupDiscountTotal"/> against it, reconciling with the
    /// (list-priced) line items the cart renders.
    /// </summary>
    public decimal BaseSubtotal { get; set; }

    /// <summary>
    /// The saving from the buyer's Customer Group pricing (<see cref="BaseSubtotal"/> − <see cref="Subtotal"/>)
    /// when positive; 0 when there is no group discount. Group pricing is applied by lowering each line's unit
    /// price, so without this it would be invisible in the summary (WO-22).
    /// </summary>
    public decimal GroupDiscountTotal { get; set; }

    /// <summary>The name of the pricing group behind <see cref="GroupDiscountTotal"/> (summary label); null when none.</summary>
    public string? GroupDiscountName { get; set; }

    public List<AppliedDiscount> Discounts { get; set; } = new();
    public decimal DiscountTotal { get; set; }
    public List<ShippingRateOption> ShippingOptions { get; set; } = new();
    public decimal ShippingTotal { get; set; }
    public TaxBreakdown Tax { get; set; } = new(0m, new(), false);
    public decimal TaxTotal { get; set; }

    /// <summary>
    /// The active tax-calculation provider (<see cref="TaxProviderType"/> name: FlatRate/TaxJar/StripeTax) so
    /// the storefront can show which partner priced the tax — see also <see cref="TaxBreakdown.FallbackApplied"/>.
    /// </summary>
    public string? TaxProvider { get; set; }

    public decimal Total { get; set; }
    public Guid? AssignedStoreId { get; set; }
    public bool IsRoutable { get; set; }
    public bool GuestOrderingAllowed { get; set; }

    /// <summary>
    /// Payment methods offered for this order — the active/enabled + credential-configured gateways, plus
    /// Cash on Delivery only when the fulfilling store allows it. Each carries its live/sandbox mode so the
    /// storefront can badge it. The storefront renders exactly these (each mapped to its own label + icon).
    /// </summary>
    public List<PaymentMethodOption> AvailablePaymentMethods { get; set; } = new();

    /// <summary>
    /// True when this cart checks out as an inquiry (REQ-INQ-001) — the tenant sells by inquiry, or the
    /// cart holds a quote-only product. Shipping options, tax and payment methods are all empty in that
    /// case, and the storefront must submit to /api/checkout/inquiry rather than /place.
    /// </summary>
    public bool IsInquiry { get; set; }
}

/// <summary>
/// One offered payment method as surfaced to the storefront: the <see cref="PaymentMethodType"/> name plus
/// <see cref="IsProduction"/> — <c>true</c> for a live credential, <c>false</c> for sandbox/test, and
/// <c>null</c> for manual methods (Cash on Delivery, Bank Transfer) that have no gateway environment.
/// </summary>
public class PaymentMethodOption
{
    public string Method { get; set; } = string.Empty;
    public bool? IsProduction { get; set; }

    /// <summary>The gateway's transaction fee as a percentage of the order total (0 when none). The storefront
    /// shows it against each method and adds it to the total the buyer pays when the method is selected.</summary>
    public decimal FeePercent { get; set; }
}

/// <summary>
/// Request to place (finalize) a checkout (REQ-CHK-003): the cart identity, the delivery
/// <see cref="ShipTo"/> address, the chosen <see cref="SelectedShippingMethodId"/>, the
/// <see cref="PaymentMethod"/> and optional gateway <see cref="PaymentToken"/>, and an optional
/// <see cref="CouponCode"/> (overrides any coupon already applied to the cart).
/// </summary>
public record PlaceCheckoutRequest(
    Guid? CartId,
    string? SessionId,
    CheckoutAddress ShipTo,
    string? SelectedShippingMethodId,
    PaymentMethodType PaymentMethod,
    string? PaymentToken,
    string? CouponCode,
    string? RecaptchaToken = null,
    bool PickupInStore = false,
    Guid? PickupStoreId = null,
    /// <summary>
    /// The instrument the <see cref="PaymentToken"/> tokenizes, when the gateway offers more than one on the
    /// same credential — "BankAccount" for an Authorize.Net ACH/eCheck payment, else null/"Card". Ignored by
    /// every other gateway. See <see cref="Domain.Common.PaymentInstruments"/>.
    /// </summary>
    string? PaymentInstrument = null);

/// <summary>
/// The outcome of placing a checkout: the created order's id/number/status and grand total, the
/// resulting payment status, and a <see cref="Success"/> flag with an optional <see cref="Error"/> —
/// a failed payment returns <see cref="Success"/> = false while leaving a retryable pending order
/// (AC-PAY-001.3).
/// </summary>
public class CheckoutResult
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>
    /// The gateway transaction id for a successful (captured) payment, surfaced on the order-confirmation
    /// screen. Null for pending/redirect/failed outcomes and for methods that produce no transaction id.
    /// </summary>
    public string? TransactionId { get; set; }

    /// <summary>
    /// For redirect gateways (Stripe Checkout): the URL to send the buyer to. When set, the storefront
    /// redirects here to complete payment; the order stays pending until they return and confirm.
    /// </summary>
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// For client-completed gateways (Razorpay Checkout): the config the storefront needs to open the
    /// on-site payment widget. When set, the storefront opens the widget and, on completion, calls
    /// confirm-client-payment to verify + capture. Null for inline/redirect gateways.
    /// </summary>
    public ClientPaymentAction? ClientPayment { get; set; }
}

/// <summary>
/// The data a client-completed gateway hands to the storefront to open its on-site payment widget
/// (Razorpay Checkout). The buyer pays inside the widget; the tokens it returns are posted back to
/// confirm-client-payment, which verifies the signature server-side and captures the payment.
/// </summary>
public class ClientPaymentAction
{
    /// <summary>The gateway this widget belongs to (e.g. "razorpay") so the storefront opens the right SDK.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The gateway's public/publishable key used to initialise the widget (never the secret).</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>The provider order id the widget charges against (Razorpay order_id), created server-side.</summary>
    public string GatewayOrderId { get; set; } = string.Empty;

    /// <summary>The amount in the currency's minor unit (paise/cents), matching the created provider order.</summary>
    public long AmountMinor { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Our order number, shown in the widget and used as the description.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Buyer contact details to prefill the widget (best-effort; the buyer can edit them).</summary>
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
}

/// <summary>
/// Request to submit an inquiry (quote request) instead of placing a paid order (REQ-INQ-001). Reuses
/// <see cref="CheckoutAddress"/> for the contact block: in contact-only mode the postal fields arrive
/// blank, but the name/email/phone still land on the order's address row — which is where
/// <c>Order.ContactEmail</c> and the notification templates read them from.
/// </summary>
public record SubmitInquiryRequest(
    Guid? CartId,
    string? SessionId,
    CheckoutAddress Contact,
    string? Message,
    string? CompanyName,
    string? PreferredContact,
    DateTime? RequiredByUtc,
    string? CouponCode = null,
    string? RecaptchaToken = null);

/// <summary>
/// The outcome of submitting an inquiry: the created record's id/reference and its indicative value. There
/// is no payment status — nothing was charged, and nothing is pending.
/// </summary>
public class InquiryResult
{
    public Guid InquiryId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>The priced value of the request (subtotal less discounts); shipping and tax are not included.</summary>
    public decimal EstimatedTotal { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The store the inquiry was assigned to, when routing or the configured fallback found one.</summary>
    public Guid? AssignedStoreId { get; set; }

    public bool Success { get; set; }
}
