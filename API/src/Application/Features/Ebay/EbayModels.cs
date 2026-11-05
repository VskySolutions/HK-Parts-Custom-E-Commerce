using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Ebay;

public enum EbayConnectionState
{
    NotConnected = 0,
    Connected = 1,
    TokenExpired = 2,
}

/// <summary>Settings as the admin sees them — tokens are never returned.</summary>
public class EbaySettingsDto
{
    public Guid StoreId { get; set; }
    public string? StoreName { get; set; }
    public EbayConnectionState ConnectionState { get; set; }
    public string? ConnectedEbayUser { get; set; }
    public DateTime? ConnectedAtUtc { get; set; }
    public bool AppCredentialsConfigured { get; set; }

    public bool IsEnabled { get; set; }
    public EbayEnvironment Environment { get; set; }
    public string MarketplaceId { get; set; } = "EBAY_US";
    public bool LoggingEnabled { get; set; }
    public string? DefaultCategoryId { get; set; }
    public string? DefaultCategoryName { get; set; }
    public string? DefaultPaymentPolicyId { get; set; }
    public string? DefaultFulfillmentPolicyId { get; set; }
    public string? DefaultReturnPolicyId { get; set; }
    public string? MerchantLocationKey { get; set; }
    public bool FitmentEnabled { get; set; }
    public bool SyncPriceFromStore { get; set; }
    public DateTime? OrdersLastSyncedAtUtc { get; set; }
    public int CategoryCount { get; set; }
    public string? CategoryTreeVersion { get; set; }
}

public class EbayCategoryDto
{
    public string CategoryId { get; set; } = string.Empty;
    public string? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
    /// <summary>Breadcrumb ("Parts &gt; Brakes &gt; Pads") — filled for search results.</summary>
    public string? Path { get; set; }

    public static EbayCategoryDto From(EbayCategory c) => new()
    {
        CategoryId = c.CategoryId,
        ParentCategoryId = c.ParentCategoryId,
        Name = c.Name,
        Level = c.Level,
        IsLeaf = c.IsLeaf,
    };
}

public class EbayEligibleProductDto
{
    public Guid Id { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductType ProductType { get; set; }
    public int StoreQuantity { get; set; }
    public decimal? Price { get; set; }
}

public class EbayListingListItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int StoreInventory { get; set; }
    public int Quantity { get; set; }
    public int QuantitySold { get; set; }
    public decimal Price { get; set; }
    public EbayListingStatus Status { get; set; }
    public string? LastError { get; set; }
    public string? EbayItemId { get; set; }
    public string? EbayItemUrl { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
}

public class EbayListingAspectValueDto
{
    public string Name { get; set; } = string.Empty;
    public List<string> Values { get; set; } = new();
}

public class EbayListingImageDto
{
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
}

public class EbayFitmentDto
{
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Trim { get; set; }
    public string? Engine { get; set; }
}

public class EbayListingDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public EbayListingStatus Status { get; set; }
    public bool IsPublished { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int QuantitySold { get; set; }
    public int StoreInventory { get; set; }
    public string? ConditionId { get; set; }
    public string? PaymentPolicyId { get; set; }
    public string? FulfillmentPolicyId { get; set; }
    public string? ReturnPolicyId { get; set; }
    public string? OfferId { get; set; }
    public string? EbayItemId { get; set; }
    public string? EbayItemUrl { get; set; }
    public string? LastError { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public List<EbayListingAspectValueDto> Aspects { get; set; } = new();
    public List<EbayListingImageDto> Images { get; set; } = new();
    public bool FitmentEnabled { get; set; }
    public List<EbayFitmentDto> Fitment { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;

    /// <summary>Snapshot for the audit log (before/after).</summary>
    public object ToAuditSnapshot() => new
    {
        Status = Status.ToString(), Title, Subtitle, Sku, CategoryId, Price, Quantity, ConditionId,
        PaymentPolicyId, FulfillmentPolicyId, ReturnPolicyId, OfferId, EbayItemId, LastError,
        Aspects = Aspects.ToDictionary(a => a.Name, a => a.Values),
    };
}

public class EbayItemConditionDto
{
    public string ConditionId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
