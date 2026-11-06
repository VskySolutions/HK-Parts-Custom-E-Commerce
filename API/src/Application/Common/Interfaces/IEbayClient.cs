using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

// ---- Models (eBay REST shapes, trimmed to what the module uses) -----------------------------------------

public sealed record EbayTokenResult(
    string AccessToken, DateTime AccessTokenExpiresAtUtc, string? RefreshToken, DateTime? RefreshTokenExpiresAtUtc);

public sealed record EbayPolicy(string Id, string Name);

public enum EbayPolicyType { Payment, Fulfillment, Return }

public sealed record EbayAddress(
    string? Name, string? AddressLine1, string? AddressLine2, string? City, string? StateOrProvince,
    string? PostalCode, string? CountryCode, string? Phone = null);

public sealed record EbayCategoryNode(string CategoryId, string? ParentCategoryId, string Name, int Level, bool IsLeaf);

public sealed record EbayCategoryTree(string TreeId, string Version, IReadOnlyList<EbayCategoryNode> Categories);

/// <summary>An item specific of a category (Taxonomy getItemAspectsForCategory).</summary>
public sealed record EbayAspectDefinition(
    string Name, bool Required, bool Multiple, bool FreeText, IReadOnlyList<string> Values, int? MaxLength = null);

public sealed record EbayCondition(string ConditionId, string Description);

public sealed record EbayCompatibilityInfo(bool Supported, int MaxCompatibleVehicles);

public sealed record EbayCompatibleVehicle(int Year, string Make, string Model, string? Trim, string? Engine);

public sealed record EbayInventoryItem(
    string Title,
    string? Subtitle,
    string Description,
    string? ConditionId,
    int Quantity,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Aspects);

public sealed record EbayOffer(
    string Sku,
    string MarketplaceId,
    string CategoryId,
    decimal Price,
    string CurrencyCode,
    int Quantity,
    string? PaymentPolicyId,
    string? FulfillmentPolicyId,
    string? ReturnPolicyId,
    string? MerchantLocationKey,
    string? ListingDescription);

public sealed record EbayPriceQuantityUpdate(string Sku, string? OfferId, int Quantity, decimal? Price, string CurrencyCode);

public sealed record EbayPriceQuantityResult(string Sku, bool Success, IReadOnlyList<ExternalError> Errors);

public sealed record EbayOrderLineItem(
    string LineItemId, string? LegacyItemId, string? Sku, string Title, int Quantity, decimal LineItemCost, decimal UnitPrice);

public sealed record EbayOrder(
    string OrderId,
    string OrderPaymentStatus,
    DateTime CreationDateUtc,
    string? BuyerUsername,
    string? BuyerEmail,
    string? BuyerMessage,
    EbayAddress? ShipTo,
    string? ShipToEmail,
    string? ShippingServiceCode,
    decimal Subtotal,
    decimal DeliveryCost,
    decimal Tax,
    decimal Total,
    string CurrencyCode,
    string? PaymentMethod,
    IReadOnlyList<EbayOrderLineItem> LineItems);

public sealed record EbayOrderPage(IReadOnlyList<EbayOrder> Orders, int Total, int Offset, int Limit);

/// <summary>A failed eBay call. <see cref="Errors"/> holds error-severity messages only (warnings dropped).</summary>
public class EbayApiException : Exception
{
    public EbayApiException(int statusCode, IReadOnlyList<ExternalError> errors, string? message = null)
        : base(message ?? (errors.Count > 0 ? errors[0].Message : $"eBay returned HTTP {statusCode}."))
    {
        StatusCode = statusCode;
        Errors = errors;
    }

    public int StatusCode { get; }
    public IReadOnlyList<ExternalError> Errors { get; }
}

// ---- Clients --------------------------------------------------------------------------------------------

/// <summary>
/// The OAuth (Identity API) half: building the consent URL and exchanging/refreshing user tokens with the
/// app credentials from configuration. Split from <see cref="IEbayClient"/> so token refresh can be tested.
/// </summary>
public interface IEbayOAuthClient
{
    /// <summary>The scopes the module requests.</summary>
    IReadOnlyList<string> Scopes { get; }

    /// <summary>False when the environment's app credentials are not configured.</summary>
    bool IsConfigured(EbayEnvironment environment);

    string BuildAuthorizeUrl(EbayEnvironment environment, string state);
    Task<EbayTokenResult> ExchangeCodeAsync(EbayEnvironment environment, string code, CancellationToken ct = default);
    Task<EbayTokenResult> RefreshAsync(EbayEnvironment environment, string refreshToken, CancellationToken ct = default);
}

/// <summary>A ready-to-use connection: a valid access token plus the account settings a call needs.</summary>
public sealed record EbaySession(
    Guid StoreId, EbayEnvironment Environment, string MarketplaceId, string AccessToken, bool LoggingEnabled);

/// <summary>
/// Resolves a store's eBay session, refreshing the access token when it is within 5 minutes of expiry.
/// Throws <see cref="ServiceUnavailableException"/> ("Reconnect eBay") when the store is not connected, is
/// disabled (unless <c>allowDisabled</c>), or the refresh fails.
/// </summary>
public interface IEbayAuthService
{
    Task<EbaySession> GetSessionAsync(Guid storeId, bool allowDisabled = false, CancellationToken ct = default);
}

/// <summary>
/// Typed eBay REST Sell API client (Inventory, Account, Taxonomy, Metadata, Fulfillment, Identity user).
/// Every call resolves the store session (auto-refresh), retries 429/5xx with backoff, and writes an
/// <c>EbayApiLog</c> row (tokens redacted) when the store's logging is on. Business failures throw
/// <see cref="EbayApiException"/>.
/// </summary>
public interface IEbayClient
{
    // Identity
    Task<string?> GetUsernameAsync(Guid storeId, CancellationToken ct = default);

    // Account
    Task<IReadOnlyList<EbayPolicy>> GetPoliciesAsync(Guid storeId, EbayPolicyType type, CancellationToken ct = default);
    Task CreateOrReplaceLocationAsync(Guid storeId, string merchantLocationKey, EbayAddress address, string name, CancellationToken ct = default);

    // Taxonomy
    Task<string> GetDefaultCategoryTreeIdAsync(Guid storeId, CancellationToken ct = default);
    Task<EbayCategoryTree> GetCategoryTreeAsync(Guid storeId, string treeId, CancellationToken ct = default);
    Task<IReadOnlyList<EbayAspectDefinition>> GetItemAspectsAsync(Guid storeId, string treeId, string categoryId, CancellationToken ct = default);

    // Metadata
    Task<IReadOnlyList<EbayCondition>> GetItemConditionsAsync(Guid storeId, string categoryId, CancellationToken ct = default);
    Task<EbayCompatibilityInfo> GetCompatibilityInfoAsync(Guid storeId, string categoryId, CancellationToken ct = default);

    // Inventory
    Task CreateOrReplaceInventoryItemAsync(Guid storeId, string sku, EbayInventoryItem item, CancellationToken ct = default);
    Task CreateOrReplaceProductCompatibilityAsync(Guid storeId, string sku, IReadOnlyList<EbayCompatibleVehicle> vehicles, CancellationToken ct = default);
    Task<string> CreateOfferAsync(Guid storeId, EbayOffer offer, CancellationToken ct = default);
    Task UpdateOfferAsync(Guid storeId, string offerId, EbayOffer offer, CancellationToken ct = default);
    Task<string> PublishOfferAsync(Guid storeId, string offerId, CancellationToken ct = default);
    Task WithdrawOfferAsync(Guid storeId, string offerId, CancellationToken ct = default);
    Task<IReadOnlyList<EbayPriceQuantityResult>> BulkUpdatePriceQuantityAsync(Guid storeId, IReadOnlyList<EbayPriceQuantityUpdate> updates, CancellationToken ct = default);

    // Fulfillment
    Task<EbayOrderPage> GetOrdersAsync(Guid storeId, DateTime modifiedSinceUtc, int offset, int limit, CancellationToken ct = default);
    Task<string> CreateShippingFulfillmentAsync(Guid storeId, string orderId, IReadOnlyList<(string LineItemId, int Quantity)> lineItems, string carrierCode, string trackingNumber, CancellationToken ct = default);
}

/// <summary>
/// Queue for eBay background work (single coalescing consumer). Work is idempotent, so a duplicate request
/// is harmless.
/// </summary>
public interface IEbayJobQueue
{
    /// <summary>Null store = every store with an enabled eBay account; null products = every active listing.</summary>
    void EnqueueInventorySync(Guid? storeId, IEnumerable<Guid>? productIds = null);
    void EnqueueOutOfStock(IEnumerable<Guid> productIds);
    void EnqueueShipmentSync(Guid shipmentId);
    void EnqueueCategoryRefresh(Guid storeId);
    void EnqueueOrderImport(Guid storeId, bool force = false);
}
