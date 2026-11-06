using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Ebay;

/// <summary>
/// eBay app credentials per environment, bound from the <c>Ebay</c> configuration section. Supply the values
/// through User Secrets / environment variables / Azure Key Vault — never commit them or store them in the
/// database. Example (User Secrets):
/// <code>
/// dotnet user-secrets set "Ebay:Sandbox:ClientId" "..."
/// dotnet user-secrets set "Ebay:Sandbox:ClientSecret" "..."
/// dotnet user-secrets set "Ebay:Sandbox:RuName" "..."
/// </code>
/// </summary>
public sealed class EbayOptions
{
    public const string SectionName = "Ebay";

    public EbayAppCredentials Sandbox { get; set; } = new();
    public EbayAppCredentials Production { get; set; } = new();

    /// <summary>
    /// Public HTTPS origin prefixed to relative media URLs (e.g. "/uploads/x.png") so eBay can fetch product
    /// images. Absolute media URLs (CDN / blob storage) are sent unchanged.
    /// </summary>
    public string? ImageBaseUrl { get; set; }

    /// <summary>Admin SPA origin the OAuth callback redirects back to; falls back to Storefront:PublicBaseUrl.</summary>
    public string? AdminBaseUrl { get; set; }

    /// <summary>OAuth scopes requested on connect.</summary>
    public List<string> Scopes { get; set; } = new()
    {
        "https://api.ebay.com/oauth/api_scope",
        "https://api.ebay.com/oauth/api_scope/sell.inventory",
        "https://api.ebay.com/oauth/api_scope/sell.account",
        "https://api.ebay.com/oauth/api_scope/sell.fulfillment",
        "https://api.ebay.com/oauth/api_scope/commerce.taxonomy.readonly",
        "https://api.ebay.com/oauth/api_scope/commerce.identity.readonly",
    };

    public EbayAppCredentials For(EbayEnvironment environment) =>
        environment == EbayEnvironment.Production ? Production : Sandbox;

    public static string ApiBase(EbayEnvironment environment) =>
        environment == EbayEnvironment.Production ? "https://api.ebay.com" : "https://api.sandbox.ebay.com";

    public static string IdentityApiBase(EbayEnvironment environment) =>
        environment == EbayEnvironment.Production ? "https://apiz.ebay.com" : "https://apiz.sandbox.ebay.com";

    public static string AuthBase(EbayEnvironment environment) =>
        environment == EbayEnvironment.Production ? "https://auth.ebay.com" : "https://auth.sandbox.ebay.com";

    public static string ItemUrl(EbayEnvironment environment, string listingId) =>
        (environment == EbayEnvironment.Production ? "https://www.ebay.com/itm/" : "https://sandbox.ebay.com/itm/") + listingId;
}

public sealed class EbayAppCredentials
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    /// <summary>The eBay "RuName" (redirect URL name) whose accept URL points at /api/admin/ebay/oauth/callback.</summary>
    public string? RuName { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RuName);
}
