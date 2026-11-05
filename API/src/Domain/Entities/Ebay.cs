using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// One store's eBay connection and listing defaults (one row per store, keyed by <see cref="StoreId"/>).
/// The eBay app credentials (client id/secret, RuName) come from configuration, never this table; the user
/// tokens here are encrypted at rest by the Data Protection column converter.
/// </summary>
public class EbayAccount : IAuditableEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    public bool IsEnabled { get; set; }
    public EbayEnvironment Environment { get; set; } = EbayEnvironment.Sandbox;
    public string MarketplaceId { get; set; } = "EBAY_US";
    public string? CategoryTreeId { get; set; }

    /// <summary>Encrypted at rest.</summary>
    [Encrypted]
    public string? AccessToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    /// <summary>Encrypted at rest.</summary>
    [Encrypted]
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
    public DateTime? ConnectedAtUtc { get; set; }
    public string? ConnectedEbayUser { get; set; }

    public bool LoggingEnabled { get; set; }
    public string? DefaultCategoryId { get; set; }
    public string? DefaultPaymentPolicyId { get; set; }
    public string? DefaultFulfillmentPolicyId { get; set; }
    public string? DefaultReturnPolicyId { get; set; }
    public string? MerchantLocationKey { get; set; }
    public bool FitmentEnabled { get; set; }

    /// <summary>Push the store's final price to active listings during inventory sync (default off: listings keep their saved price).</summary>
    public bool SyncPriceFromStore { get; set; }

    public DateTime? OrdersLastSyncedAtUtc { get; set; }

    public Guid? CreatedById { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public Guid? UpdatedById { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}

/// <summary>A product listed (or being drafted) on eBay through the Inventory API (inventory item + offer).</summary>
public class EbayListing : AuditableEntity
{
    public Guid StoreId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>The eBay inventory item key.</summary>
    public string Sku { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public EbayListingStatus Status { get; set; } = EbayListingStatus.Draft;

    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int QuantitySold { get; set; }
    public string? ConditionId { get; set; }

    public string? PaymentPolicyId { get; set; }
    public string? FulfillmentPolicyId { get; set; }
    public string? ReturnPolicyId { get; set; }

    public string? OfferId { get; set; }
    /// <summary>The eBay item/listing id; null until published.</summary>
    public string? EbayListingIdExternal { get; set; }
    public string? LastError { get; set; }

    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<EbayListingAspect> Aspects { get; set; } = new List<EbayListingAspect>();
}

/// <summary>One item-specific value; a multi-value aspect is several rows with the same <see cref="Name"/>.</summary>
public class EbayListingAspect
{
    public long Id { get; set; }
    public Guid EbayListingId { get; set; }
    public EbayListing? EbayListing { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>A node of the marketplace category tree, downloaded from the Taxonomy API.</summary>
public class EbayCategory
{
    public string CategoryId { get; set; } = string.Empty;
    public string? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
    public string? TreeVersion { get; set; }
}

/// <summary>One eBay API call, written only while the store's API logging is on. Tokens are redacted.</summary>
public class EbayApiLog
{
    public long Id { get; set; }
    public Guid StoreId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? RequestBody { get; set; }
    public int? ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
    public bool Success { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}

/// <summary>A vehicle a part fits (sent to eBay as product compatibility for supporting categories).</summary>
public class VehicleFitment : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Trim { get; set; }
    public string? Engine { get; set; }
}
