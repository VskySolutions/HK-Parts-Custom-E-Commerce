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
}

/// <summary>UPS OAuth2 client credentials + merchant/account number + endpoint (table <c>Credentials_UPS</c>).</summary>
public class UpsCredential : IntegrationCredentialBase
{
    /// <summary>API host this credential belongs to — sandbox and live have different hosts. Required.</summary>
    public string? BaseUrl { get; set; }
    public string? MerchantId { get; set; }
    public string? ClientId { get; set; }
    [Encrypted] public string? ClientSecret { get; set; }
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
