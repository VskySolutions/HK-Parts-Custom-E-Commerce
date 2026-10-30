using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Full detail view of a catalog product including every child collection (REQ-CAT-001).</summary>
public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public ProductType ProductType { get; set; }
    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int StockQuantity { get; set; }
    public bool AllowBackorder { get; set; }
    public DateTime? EstimatedRestockDate { get; set; }
    public Guid TaxCategoryId { get; set; }
    public Guid? ManufacturerId { get; set; }
    public bool IsPublished { get; set; }
    public bool ReviewsEnabled { get; set; }

    /// <summary>Quote-only product (REQ-INQ-001): the storefront requests a quote instead of selling it.</summary>
    public bool IsInquiryOnly { get; set; }

    /// <summary>Per-product override for the inquiry call-to-action; null uses the tenant default.</summary>
    public string? InquiryButtonLabel { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
    public int FeaturedDisplayOrder { get; set; }
    public int? DownloadExpiryDays { get; set; }
    public int? DownloadLimit { get; set; }
    public GiftCardType? GiftCardType { get; set; }
    public decimal? GiftCardAmount { get; set; }
    public string? H1 { get; set; }
    public Guid? MetaImageMediaId { get; set; }
    public string? MetaOpenGraph { get; set; }
    public string? MetaCustomTags { get; set; }
    public string? MetaFooterTags { get; set; }
    public string? Gtin { get; set; }
    public decimal? Cost { get; set; }
    public decimal? Msrp { get; set; }
    public decimal? SalePrice { get; set; }
    public DateTime? SaleStartDateUtc { get; set; }
    public DateTime? SaleEndDateUtc { get; set; }
    public string? TaxCode { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsFflRequired { get; set; }
    public int? MinOrderQuantity { get; set; }
    public int? MaxOrderQuantity { get; set; }
    public StockStatus? StockStatusOverride { get; set; }
    public string? BackorderMessage { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public int PackagePieces { get; set; }
    public string? PackageType { get; set; }
    public string? FreightClass { get; set; }
    public bool IsHazardous { get; set; }
    /// <summary>Resolved public URL of <see cref="MetaImageMediaId"/>.</summary>
    public string? MetaImageUrl { get; set; }
    /// <summary>Stores the product is listed in; empty = every store.</summary>
    public List<Guid> StoreIds { get; set; } = new();
    public List<ProductCustomFieldValueDto> CustomFieldValues { get; set; } = new();

    public List<ProductVariantDto> Variants { get; set; } = new();
    public List<ProductPictureDto> Pictures { get; set; } = new();
    public List<TierPriceDto> TierPrices { get; set; } = new();
    public List<Guid> CategoryIds { get; set; } = new();
    public List<ProductTagDto> Tags { get; set; } = new();
    public List<Guid> AttributeIds { get; set; } = new();
    public List<Guid> SpecificationOptionIds { get; set; } = new();
    public List<ProductRelationshipDto> Relationships { get; set; } = new();
    public List<Guid> GroupedMemberIds { get; set; } = new();
    public List<ProductDownloadDto> Downloads { get; set; } = new();

    /// <summary>Projects a fully-loaded product graph into a DTO (see <c>ProductQueries.WithFullGraph</c>).</summary>
    public static ProductDto From(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        ProductType = p.ProductType,
        ShortDescription = p.ShortDescription,
        FullDescription = p.FullDescription,
        MetaTitle = p.MetaTitle,
        MetaDescription = p.MetaDescription,
        MetaKeywords = p.MetaKeywords,
        Sku = p.Sku,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        AllowBackorder = p.AllowBackorder,
        EstimatedRestockDate = p.EstimatedRestockDate,
        TaxCategoryId = p.TaxCategoryId,
        ManufacturerId = p.ManufacturerId,
        IsPublished = p.IsPublished,
        ReviewsEnabled = p.ReviewsEnabled,
        IsInquiryOnly = p.IsInquiryOnly,
        InquiryButtonLabel = p.InquiryButtonLabel,
        DisplayOrder = p.DisplayOrder,
        IsFeatured = p.IsFeatured,
        FeaturedDisplayOrder = p.FeaturedDisplayOrder,
        DownloadExpiryDays = p.DownloadExpiryDays,
        DownloadLimit = p.DownloadLimit,
        GiftCardType = p.GiftCardType,
        GiftCardAmount = p.GiftCardAmount,
        H1 = p.H1,
        MetaImageMediaId = p.MetaImageMediaId,
        MetaOpenGraph = p.MetaOpenGraph,
        MetaCustomTags = p.MetaCustomTags,
        MetaFooterTags = p.MetaFooterTags,
        Gtin = p.Gtin,
        Cost = p.Cost,
        Msrp = p.Msrp,
        SalePrice = p.SalePrice,
        SaleStartDateUtc = p.SaleStartDateUtc,
        SaleEndDateUtc = p.SaleEndDateUtc,
        TaxCode = p.TaxCode,
        IsPurchasable = p.IsPurchasable,
        IsFflRequired = p.IsFflRequired,
        MinOrderQuantity = p.MinOrderQuantity,
        MaxOrderQuantity = p.MaxOrderQuantity,
        StockStatusOverride = p.StockStatusOverride,
        BackorderMessage = p.BackorderMessage,
        Weight = p.Weight,
        Length = p.Length,
        Width = p.Width,
        Height = p.Height,
        PackagePieces = p.PackagePieces,
        PackageType = p.PackageType,
        FreightClass = p.FreightClass,
        IsHazardous = p.IsHazardous,
        MetaImageUrl = p.MetaImage?.Url,
        StoreIds = p.StoreMappings.Select(m => m.StoreId).ToList(),
        CustomFieldValues = p.CustomFieldValues.Select(ProductCustomFieldValueDto.From).ToList(),
        Variants = p.Variants
            .OrderBy(v => v.DisplayOrder)
            .Select(ProductVariantDto.From)
            .ToList(),
        Pictures = p.Pictures
            .OrderBy(i => i.DisplayOrder)
            .Select(ProductPictureDto.From)
            .ToList(),
        TierPrices = p.TierPrices
            .Select(TierPriceDto.From)
            .Concat(p.Variants.SelectMany(v => v.TierPrices).Select(TierPriceDto.From))
            .OrderBy(t => t.ProductVariantId)
            .ThenBy(t => t.MinQuantity)
            .ToList(),
        CategoryIds = p.ProductCategories
            .OrderBy(c => c.DisplayOrder)
            .Select(c => c.CategoryId)
            .ToList(),
        Tags = p.Tags
            .Where(t => t.ProductTag is not null)
            .Select(t => ProductTagDto.From(t.ProductTag!))
            .OrderBy(t => t.Name)
            .ToList(),
        AttributeIds = p.AttributeMappings
            .OrderBy(a => a.DisplayOrder)
            .Select(a => a.ProductAttributeId)
            .ToList(),
        SpecificationOptionIds = p.SpecificationValues
            .Select(s => s.SpecificationAttributeOptionId)
            .ToList(),
        Relationships = p.Relationships
            .OrderBy(r => r.RelationshipType)
            .ThenBy(r => r.DisplayOrder)
            .Select(ProductRelationshipDto.From)
            .ToList(),
        GroupedMemberIds = p.GroupedMembers
            .OrderBy(g => g.DisplayOrder)
            .Select(g => g.MemberProductId)
            .ToList(),
        Downloads = p.Downloads
            .OrderBy(d => d.DisplayOrder)
            .Select(ProductDownloadDto.From)
            .ToList(),
    };
}

/// <summary>Lightweight product row for paged list results.</summary>
public class ProductListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public ProductType ProductType { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    public Guid TaxCategoryId { get; set; }
    public Guid? ManufacturerId { get; set; }
    public string? Gtin { get; set; }
    /// <summary>First image (by display order); requires <c>Pictures.Media</c> to be loaded.</summary>
    public string? ImageUrl { get; set; }

    public static ProductListItemDto From(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        ProductType = p.ProductType,
        Sku = p.Sku,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        IsPublished = p.IsPublished,
        IsFeatured = p.IsFeatured,
        DisplayOrder = p.DisplayOrder,
        TaxCategoryId = p.TaxCategoryId,
        ManufacturerId = p.ManufacturerId,
        Gtin = p.Gtin,
        ImageUrl = p.Pictures
            .Where(i => i.Media != null && i.Media.MediaType == MediaType.Image)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.Media!.Url)
            .FirstOrDefault(),
    };
}

/// <summary>A purchasable variant defined by a combination of attribute values (REQ-CAT-002).</summary>
public class ProductVariantDto
{
    public Guid Id { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public string? Gtin { get; set; }
    public decimal? Cost { get; set; }
    public decimal? Msrp { get; set; }
    public decimal? Weight { get; set; }
    public int StockQuantity { get; set; }
    public bool AllowBackorder { get; set; }
    public bool IsEnabled { get; set; }
    public int DisplayOrder { get; set; }
    public List<Guid> AttributeValueIds { get; set; } = new();

    /// <summary>Projects a variant; expects <c>AttributeValues</c> to be loaded.</summary>
    public static ProductVariantDto From(ProductVariant v) => new()
    {
        Id = v.Id,
        Sku = v.Sku,
        Price = v.Price,
        Gtin = v.Gtin,
        Cost = v.Cost,
        Msrp = v.Msrp,
        Weight = v.Weight,
        StockQuantity = v.StockQuantity,
        AllowBackorder = v.AllowBackorder,
        IsEnabled = v.IsEnabled,
        DisplayOrder = v.DisplayOrder,
        AttributeValueIds = v.AttributeValues
            .Select(a => a.ProductAttributeValueId)
            .ToList(),
    };
}

/// <summary>A quantity-break price for a product or variant (REQ-CAT-006).</summary>
public class TierPriceDto
{
    public Guid Id { get; set; }
    public Guid? ProductVariantId { get; set; }
    public int MinQuantity { get; set; }
    public decimal Price { get; set; }

    public static TierPriceDto From(TierPrice t) => new()
    {
        Id = t.Id,
        ProductVariantId = t.ProductVariantId,
        MinQuantity = t.MinQuantity,
        Price = t.Price,
    };
}

/// <summary>A directional relationship to another product (REQ-CAT-007).</summary>
public class ProductRelationshipDto
{
    public Guid Id { get; set; }
    public Guid RelatedProductId { get; set; }
    public ProductRelationshipType RelationshipType { get; set; }
    public int DisplayOrder { get; set; }

    public static ProductRelationshipDto From(ProductRelationship r) => new()
    {
        Id = r.Id,
        RelatedProductId = r.RelatedProductId,
        RelationshipType = r.RelationshipType,
        DisplayOrder = r.DisplayOrder,
    };
}

/// <summary>A tag assigned to a product (REQ-CAT-008).</summary>
public class ProductTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }

    public static ProductTagDto From(ProductTag t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Slug = t.Slug,
    };
}

/// <summary>A downloadable file/URL attached to a downloadable product (AC-CAT-001.4).</summary>
public class ProductDownloadDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }

    public static ProductDownloadDto From(ProductDownload d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Url = d.Url,
        DisplayOrder = d.DisplayOrder,
    };
}

/// <summary>A product's value for one custom field.</summary>
public class ProductCustomFieldValueDto
{
    public Guid ProductCustomFieldId { get; set; }
    public string? Value { get; set; }

    public static ProductCustomFieldValueDto From(ProductCustomFieldValue v) => new()
    {
        ProductCustomFieldId = v.ProductCustomFieldId,
        Value = v.Value,
    };
}
