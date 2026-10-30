using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A catalog product of any of the five <see cref="ProductType"/> kinds (REQ-CAT-001). Simple
/// products carry SKU/price/stock directly; variant products delegate those to <see cref="ProductVariant"/>;
/// grouped products aggregate member products; downloadable and gift-card products carry type-specific config.
/// Every product must reference a <see cref="TaxCategory"/> (AC-CAT-001.6).
/// </summary>
public class Product : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public ProductType ProductType { get; set; } = ProductType.Simple;

    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }

    // SEO metadata (REQ-CAT-001) — overrides the name/description in the storefront page's
    // search-engine markup. Fall back to Name / ShortDescription when null.
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }

    /// <summary>Page heading override; the storefront falls back to <see cref="Name"/>.</summary>
    public string? H1 { get; set; }
    /// <summary>Social-share (og:image) picture from the media library.</summary>
    public Guid? MetaImageMediaId { get; set; }
    public Media? MetaImage { get; set; }
    /// <summary>Raw Open Graph markup that replaces the generated og: tags.</summary>
    public string? MetaOpenGraph { get; set; }
    /// <summary>Raw tags/scripts injected into the page &lt;head&gt;.</summary>
    public string? MetaCustomTags { get; set; }
    /// <summary>Raw tags/scripts injected before &lt;/body&gt;.</summary>
    public string? MetaFooterTags { get; set; }

    // Simple-product fields (also the default/base for other types).
    public string? Sku { get; set; }
    public decimal? Price { get; set; }

    /// <summary>Barcode (UPC / EAN / ISBN).</summary>
    public string? Gtin { get; set; }
    /// <summary>Merchant cost — internal, never shown on the storefront.</summary>
    public decimal? Cost { get; set; }
    /// <summary>Manufacturer's suggested retail ("compare at") price.</summary>
    public decimal? Msrp { get; set; }

    /// <summary>Scheduled sale price; applies while now is inside the optional start/end window.</summary>
    public decimal? SalePrice { get; set; }
    public DateTime? SaleStartDateUtc { get; set; }
    public DateTime? SaleEndDateUtc { get; set; }

    /// <summary>Tax provider product code (Avalara / TaxJar / Stripe Tax); <see cref="TaxCategoryId"/> stays the internal class.</summary>
    public string? TaxCode { get; set; }

    /// <summary>False shows the product but hides the buy button (catalog-only listing).</summary>
    public bool IsPurchasable { get; set; } = true;
    /// <summary>Firearm that must ship to a Federal Firearms Licensee.</summary>
    public bool IsFflRequired { get; set; }

    // Order-quantity limits (per cart line); null = no limit.
    public int? MinOrderQuantity { get; set; }
    public int? MaxOrderQuantity { get; set; }

    /// <summary>Manual stock-status override; null derives availability from inventory.</summary>
    public StockStatus? StockStatusOverride { get; set; }
    /// <summary>Message shown on the product page while the product is backordered.</summary>
    public string? BackorderMessage { get; set; }

    // Shipping — weight in pounds, dimensions in inches.
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public int PackagePieces { get; set; } = 1;
    /// <summary>LTL package type (pallet, box, crate…).</summary>
    public string? PackageType { get; set; }
    /// <summary>NMFC freight class (50–500).</summary>
    public string? FreightClass { get; set; }
    public bool IsHazardous { get; set; }

    /// <summary>
    /// On-hand stock is held per store in <see cref="InventoryLevels"/> (the single source of truth read
    /// by the routing engine). This is a read-through rollup = the sum across every store (and, for a
    /// variant product, across every variant). Requires <see cref="InventoryLevels"/> to be loaded.
    /// </summary>
    [NotMapped]
    public int StockQuantity => InventoryLevels?.Sum(l => l.StockQuantity) ?? 0;

    /// <summary>Whether this product may be ordered when its inventory reaches zero (AC-CAT-011.3).</summary>
    public bool AllowBackorder { get; set; }
    public DateTime? EstimatedRestockDate { get; set; }

    // Tax (required) and manufacturer (optional).
    public Guid TaxCategoryId { get; set; }
    public TaxCategory? TaxCategory { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }

    public bool IsPublished { get; set; }
    public bool ReviewsEnabled { get; set; } = true;

    /// <summary>
    /// Quote-only product (REQ-INQ-001): it can be added to the cart and priced, but the cart checks out
    /// as an inquiry rather than a payment. Ignored when the tenant is already in
    /// <see cref="Enums.CommerceMode.InquiryOnly"/> — there, every product behaves this way.
    /// </summary>
    public bool IsInquiryOnly { get; set; }

    /// <summary>Storefront call-to-action for an inquiry-only product; falls back to the tenant default.</summary>
    public string? InquiryButtonLabel { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Featured designation for the storefront (REQ-CNT-011). Default not featured.</summary>
    public bool IsFeatured { get; set; }
    /// <summary>Ordering among featured products (AC-CNT-011.2); only meaningful when <see cref="IsFeatured"/>.</summary>
    public int FeaturedDisplayOrder { get; set; }

    /// <summary>Whether this product can be subscribed to for recurring delivery (WO-49), and the CSV of
    /// allowed recurrence intervals (<see cref="SubscriptionInterval"/> member names, e.g. "Weekly,Monthly").</summary>
    public bool IsSubscribable { get; set; }
    public string? SubscriptionIntervals { get; set; }

    // Downloadable-product config (AC-CAT-001.4).
    public int? DownloadExpiryDays { get; set; }
    public int? DownloadLimit { get; set; }

    // Gift-card config (AC-CAT-001.5).
    public GiftCardType? GiftCardType { get; set; }
    public decimal? GiftCardAmount { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    // Navigation collections.
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    /// <summary>Media-library-backed pictures (WO-123): the unified image + video assignment (REQ-CAT-012).</summary>
    public ICollection<ProductPicture> Pictures { get; set; } = new List<ProductPicture>();
    public ICollection<TierPrice> TierPrices { get; set; } = new List<TierPrice>();
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
    public ICollection<ProductTagMapping> Tags { get; set; } = new List<ProductTagMapping>();
    public ICollection<ProductAttributeMapping> AttributeMappings { get; set; } = new List<ProductAttributeMapping>();
    public ICollection<ProductSpecificationValue> SpecificationValues { get; set; } = new List<ProductSpecificationValue>();
    public ICollection<ProductRelationship> Relationships { get; set; } = new List<ProductRelationship>();
    public ICollection<GroupedProductMember> GroupedMembers { get; set; } = new List<GroupedProductMember>();
    public ICollection<ProductDownload> Downloads { get; set; } = new List<ProductDownload>();
    public ICollection<InventoryLevel> InventoryLevels { get; set; } = new List<InventoryLevel>();
    /// <summary>Stores the product is listed in; empty = every store.</summary>
    public ICollection<ProductStoreMapping> StoreMappings { get; set; } = new List<ProductStoreMapping>();
    public ICollection<ProductCustomFieldValue> CustomFieldValues { get; set; } = new List<ProductCustomFieldValue>();
}
