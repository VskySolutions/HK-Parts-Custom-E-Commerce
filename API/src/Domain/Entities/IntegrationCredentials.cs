using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Shared shape of every per-integration credential row. Each concrete integration maps to its own table
/// (<c>Credentials_*</c>) so its fields are strongly typed rather than stored as generic key/value pairs.
/// A deployment may hold several rows per integration (e.g. a live and a sandbox account); the single row
/// with <see cref="Active"/> set is the one resolved at runtime. Secret fields are encrypted at rest via
/// the <see cref="EncryptedAttribute"/> value converter.
/// </summary>
public abstract class IntegrationCredentialBase : AuditableEntity, ISoftDeletable
{
    /// <summary>When set, this is the row used at runtime for its integration (at most one per integration).</summary>
    public bool Active { get; set; }

    /// <summary>Live (production) vs. sandbox/test credential — informational, surfaced in the admin list.</summary>
    public bool IsProduction { get; set; }

    /// <summary>Admin-facing label distinguishing rows of the same integration, e.g. "Live Stripe".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional transaction/processing fee charged as a percentage of the order total when this integration
    /// is used (0–100; null = no fee). Only meaningful for payment gateways — the checkout adds the active
    /// gateway credential's fee to the order total as an additional charge. Ignored by non-payment integrations.
    /// </summary>
    public decimal? TransactionFeePercent { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}

// ---- Payment gateways ------------------------------------------------------

/// <summary>Stripe API keys + redirect return URL (table <c>Credentials_Stripe</c>).</summary>
public class StripeCredential : IntegrationCredentialBase
{
    public string? PublishableKey { get; set; }
    [Encrypted] public string? SecretKey { get; set; }

    /// <summary>Full storefront URL Stripe Checkout returns the buyer to (success + cancel land here).</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>PayPal REST app credentials + endpoint (table <c>Credentials_PayPal</c>).</summary>
public class PayPalCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to — sandbox and live have different hosts. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? ClientId { get; set; }
    [Encrypted] public string? SecretKey { get; set; }

    /// <summary>Storefront URL PayPal returns the buyer to after approving/cancelling payment (the buyer lands
    /// here to confirm the order). Required for the redirect flow, mirroring the Stripe return URL.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>Razorpay key id/secret + endpoint (table <c>Credentials_Razorpay</c>).</summary>
public class RazorpayCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? KeyId { get; set; }
    [Encrypted] public string? KeySecret { get; set; }
}

/// <summary>Square application credentials + endpoint (table <c>Credentials_Square</c>).</summary>
public class SquareCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to — sandbox and live have different hosts. Required.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The Square Application ID. Public (not a secret): the storefront's Web Payments SDK needs it — with
    /// the <see cref="LocationId"/> — to render the card field and tokenize the card into a nonce.
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// The Square Location payments are taken at. Public: the Web Payments SDK needs it to initialise, and it
    /// is sent on CreatePayment so the charge is attributed to the right location. Optional — omitting it
    /// charges the account's main location.
    /// </summary>
    public string? LocationId { get; set; }

    [Encrypted] public string? AccessToken { get; set; }
    [Encrypted] public string? ApplicationSecret { get; set; }
}

/// <summary>Authorize.Net merchant credentials + endpoint (table <c>Credentials_AuthorizeNet</c>).</summary>
public class AuthorizeNetCredential : IntegrationCredentialBase
{
    /// <summary>API endpoint this credential belongs to — sandbox and live differ. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? ApplicationLoginId { get; set; }
    [Encrypted] public string? TransactionKey { get; set; }
    [Encrypted] public string? SignatureKey { get; set; }

    /// <summary>
    /// The Accept.js Public Client Key. Public (not a secret): the storefront's Accept.js library needs it —
    /// with the <see cref="ApplicationLoginId"/> — to tokenize the entered card into an opaque-data nonce in
    /// the browser, so the raw card number never reaches the server. Generated in the Authorize.Net Merchant
    /// Interface (Account → Settings → Manage Public Client Key); distinct from the Transaction/Signature keys.
    /// </summary>
    public string? PublicClientKey { get; set; }

    // ---- Payment-method settings (authorize-net-payment-requirements.md §2 / §4) ----
    // Kept on the credential row, so each environment (sandbox / production row) carries its own settings and
    // switching the active row never points live keys at the sandbox (AN-02).

    /// <summary>"AuthCapture" (authCaptureTransaction) or "AuthOnly" (authOnlyTransaction). Null = AuthCapture (AN-13).</summary>
    public string? TransactionType { get; set; }

    /// <summary>Create a CIM customer + payment profile from each successful sale (AN-30/31).</summary>
    public bool CustomerProfilesEnabled { get; set; }

    /// <summary>Log each gateway call (type, amount, result, codes, transaction id, duration). No nonce or keys (AN-55).</summary>
    public bool LoggingEnabled { get; set; }

    /// <summary>
    /// Comma-separated card brands accepted, as Authorize.Net reports accountType (Visa, MasterCard,
    /// AmericanExpress, Discover, JCB, DinersClub). Null/empty = all. Enforced against the returned account type.
    /// </summary>
    public string? AcceptedCardTypes { get; set; }

    /// <summary>Name shown to the buyer at checkout (legacy: "Credit Card").</summary>
    public string? DisplayName { get; set; }

    /// <summary>Offer the method only when the amount due is at least this (null = no minimum).</summary>
    public decimal? MinOrderAmount { get; set; }

    /// <summary>Offer the method only when the amount due is at most this (null = no maximum).</summary>
    public decimal? MaxOrderAmount { get; set; }

    /// <summary>Comma-separated customer-group ids allowed to use the method. Null/empty = all customers.</summary>
    public string? AllowedCustomerGroupIds { get; set; }

    /// <summary>Order status after an approved payment: "Pending" (default) or "Processing".</summary>
    public string? DefaultOrderStatus { get; set; }
}

// ---- Tax providers ---------------------------------------------------------

/// <summary>TaxJar API token (table <c>Credentials_TaxJar</c>).</summary>
public class TaxJarCredential : IntegrationCredentialBase
{
    public string? BaseUrl { get; set; }
    [Encrypted] public string? SecretKey { get; set; }
}

/// <summary>Stripe Tax secret key + endpoint (table <c>Credentials_StripeTax</c>).</summary>
public class StripeTaxCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? PublishableKey { get; set; }
    [Encrypted] public string? SecretKey { get; set; }
}

// ---- Shipping carriers -----------------------------------------------------

/// <summary>FedEx API credentials + account + endpoint (table <c>Credentials_FedEx</c>).</summary>
public class FedExCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to — sandbox and live have different hosts. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    [Encrypted] public string? ApiSecret { get; set; }

    /// <summary>
    /// The FedEx account the shipment rates against. Required to quote: the key/secret only authenticate
    /// the caller, and a rate request that names no account is rejected with 403 — which reads exactly
    /// like bad credentials, so an absent account number is worth ruling out first.
    /// </summary>
    public string? AccountNumber { get; set; }

    // ---- Rating settings (docs/fedex-shipping-requirements.md §2, §4) ----

    /// <summary>Prefix of every FedEx option at checkout: "&lt;DisplayName&gt; - &lt;service&gt;" (FX-14).</summary>
    public string? DisplayName { get; set; }

    /// <summary>"ACCOUNT" (discounted) or "LIST" — the one rate type requested and read (FX-13).</summary>
    public string? RateMode { get; set; }

    /// <summary>Comma-separated FedEx service types offered (FX-14, FX-19, §3.2).</summary>
    public string? AllowedServices { get; set; }

    /// <summary>
    /// Comma-separated service types allowed for a cart holding FFL items (FX-21, [ENH]). Empty = no extra
    /// filter, pending the business decision.
    /// </summary>
    public string? FflAllowedServices { get; set; }

    /// <summary>"USE_SCHEDULED_PICKUP" or "DROPOFF_AT_FEDEX_LOCATION" (FX-50) — it changes the rate.</summary>
    public string? PickupType { get; set; }

    /// <summary>One package per unit with product dimensions; otherwise one package of the total weight (FX-16).</summary>
    public bool SendPackageInfo { get; set; }

    /// <summary>Package used when a product has no dimensions (inches). Replaces the legacy fixed 11×11×11.</summary>
    public decimal? DefaultLengthIn { get; set; }
    public decimal? DefaultWidthIn { get; set; }
    public decimal? DefaultHeightIn { get; set; }

    /// <summary>Added to every FedEx price (FX-15). Null = no fee.</summary>
    public decimal? HandlingFee { get; set; }

    /// <summary>FedEx is not offered below this order subtotal. Null = no minimum.</summary>
    public decimal? MinimumOrderAmount { get; set; }

    /// <summary>Comma-separated ISO-2 countries FedEx ships to; blank = all.</summary>
    public string? ShipToCountries { get; set; }

    /// <summary>Log each FedEx request/response (tokens and secrets never included) (FX-56).</summary>
    public bool LoggingEnabled { get; set; }
}

/// <summary>DHL Express API credentials + account + endpoint (table <c>Credentials_DHLExpress</c>).</summary>
public class DhlExpressCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to — sandbox and live have different hosts. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    [Encrypted] public string? ApiSecret { get; set; }

    /// <summary>
    /// The DHL Express account number quoted against, sent as the shipper account on a rate request.
    /// Without it MyDHL returns only generic products, if anything at all.
    /// </summary>
    public string? AccountNumber { get; set; }
}

/// <summary>USPS consumer key/secret + endpoint (table <c>Credentials_USPS</c>).</summary>
public class UspsCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to — sandbox and live have different hosts. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? ConsumerKey { get; set; }
    [Encrypted] public string? ConsumerSecret { get; set; }

    // ---- Rating settings (docs/usps-shipping-requirements.md §2, §4) ----

    /// <summary>Prefix of every USPS option at checkout: "&lt;DisplayName&gt; - &lt;service&gt;" (US-13, US-17).</summary>
    public string? DisplayName { get; set; }

    /// <summary>"RETAIL" or "COMMERCIAL" — sent as the USPS priceType.</summary>
    public string? PriceType { get; set; }

    /// <summary>Comma-separated USPS REST mail classes offered domestically (US-13, US-16).</summary>
    public string? AllowedDomesticServices { get; set; }

    /// <summary>Comma-separated USPS REST international mail classes (US-24).</summary>
    public string? AllowedInternationalServices { get; set; }

    /// <summary>Added to every domestic USPS price (US-14). Null = no fee.</summary>
    public decimal? HandlingFee { get; set; }

    /// <summary>USPS is not offered below this order subtotal. Null = no minimum.</summary>
    public decimal? MinimumOrderAmount { get; set; }

    /// <summary>Comma-separated ISO-2 countries the USPS method ships to; blank = all.</summary>
    public string? ShipToCountries { get; set; }

    /// <summary>Default package used when the cart carries no dimensions (inches). Replaces the fixed 15×15×15.</summary>
    public decimal? DefaultLengthIn { get; set; }
    public decimal? DefaultWidthIn { get; set; }
    public decimal? DefaultHeightIn { get; set; }

    /// <summary>Log each USPS request/response (secrets and tokens never included) (US-56).</summary>
    public bool LoggingEnabled { get; set; }
}

/// <summary>UPS OAuth2 client credentials + merchant/account number + endpoint (table <c>Credentials_UPS</c>).</summary>
public class UpsCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to — sandbox and live have different hosts. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? MerchantId { get; set; }
    public string? ClientId { get; set; }
    [Encrypted] public string? ClientSecret { get; set; }

    // ---- Rating settings (docs/ups-shipping-requirements.md §2, §4) ----

    /// <summary>Rating API version segment, e.g. "v2409" (sources disagree on the current one — confirm in the catalog).</summary>
    public string? ApiVersion { get; set; }

    /// <summary>Prefix of every UPS option at checkout: "&lt;DisplayName&gt; - &lt;service&gt;" (UP-14).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Comma-separated UPS service codes offered (UP-14, §3.2).</summary>
    public string? AllowedServices { get; set; }

    /// <summary>Comma-separated service codes allowed for a cart holding FFL items (UP-22/23). Default 01,13,14.</summary>
    public string? FflAllowedServices { get; set; }

    /// <summary>Use the negotiated shipment total when UPS returns one (UP-15).</summary>
    public bool UseNegotiatedRates { get; set; }

    /// <summary>UPS packaging code, e.g. "02" customer-supplied package (UP-13).</summary>
    public string? PackageType { get; set; }

    /// <summary>One package per unit with its own dimensions and weight; otherwise one package of the total weight (UP-13).</summary>
    public bool SendPackageInfo { get; set; }

    /// <summary>UPS pickup type code sent with the request, e.g. "01" daily pickup (UP-53).</summary>
    public string? PickupType { get; set; }

    /// <summary>Added to every UPS price (UP-16). Null = no fee.</summary>
    public decimal? HandlingFee { get; set; }

    /// <summary>UPS is not offered below this order subtotal. Null = no minimum.</summary>
    public decimal? MinimumOrderAmount { get; set; }

    /// <summary>Comma-separated ISO-2 countries UPS ships to; blank = all.</summary>
    public string? ShipToCountries { get; set; }

    /// <summary>Log each UPS request/response (tokens and secrets never included) (UP-57).</summary>
    public bool LoggingEnabled { get; set; }
}

/// <summary>
/// Freightview LTL freight-quote API key + the pickup/dock details sent with every quote
/// (table <c>Credentials_Freightview</c>).
/// </summary>
public class FreightviewCredential : IntegrationCredentialBase
{
    /// <summary>API host + version path, e.g. https://www.freightview.com/api/v1.0. Required.</summary>
    public string? BaseUrl { get; set; }
    [Encrypted] public string? ApiKey { get; set; }

    /// <summary>Origin location type, e.g. "business dock" or "business no dock".</summary>
    public string? OriginType { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? PickupInstructions { get; set; }
    /// <summary>Dock hours as Freightview expects them, e.g. "8:00 AM" / "5:00 PM".</summary>
    public string? DockOpenTime { get; set; }
    public string? DockCloseTime { get; set; }

    // Common method settings (AD-04), as on the USPS / UPS / FedEx methods.
    /// <summary>Prefix of every Freightview option at checkout: "&lt;DisplayName&gt; - &lt;carrier&gt; - &lt;service&gt;". Null = no prefix.</summary>
    public string? DisplayName { get; set; }
    /// <summary>Added to every Freightview quote. Null = none.</summary>
    public decimal? HandlingFee { get; set; }
    /// <summary>Offer freight only when the order subtotal is at least this. Null = no minimum.</summary>
    public decimal? MinimumOrderAmount { get; set; }
    /// <summary>Comma-separated ISO country codes freight ships to. Null/empty = all countries.</summary>
    public string? ShipToCountries { get; set; }
}

// ---- Communication ---------------------------------------------------------

/// <summary>Twilio account credentials (table <c>Credentials_Twilio</c>).</summary>
public class TwilioCredential : IntegrationCredentialBase
{
    public string? AccountSid { get; set; }
    [Encrypted] public string? AuthToken { get; set; }
    public string? WhatsAppNumber { get; set; }
}

// ---- Storage ---------------------------------------------------------------

/// <summary>Azure Blob storage connection string (table <c>Credentials_AzureBlob</c>).</summary>
public class AzureBlobCredential : IntegrationCredentialBase
{
    [Encrypted] public string? ConnectionString { get; set; }
}
