using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A purchasable variant of a <see cref="ProductType.WithVariants"/> product, generated from a
/// combination of product-attribute values (REQ-CAT-002). Carries its own SKU, price, stock and
/// backorder policy, and may have its own image set (AC-CAT-002.3/4/5).
/// </summary>
public class ProductVariant : AuditableEntity, ISoftDeletable
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public string? Gtin { get; set; }
    public decimal? Cost { get; set; }
    public decimal? Msrp { get; set; }
    /// <summary>Pounds; null inherits the product weight.</summary>
    public decimal? Weight { get; set; }

    /// <summary>Read-through rollup of this variant's per-store <see cref="InventoryLevels"/> (sum across
    /// stores). On-hand stock is held per store in the inventory; requires the collection to be loaded.</summary>
    [NotMapped]
    public int StockQuantity => InventoryLevels?.Sum(l => l.StockQuantity) ?? 0;

    /// <summary>Per-variant backorder policy (AC-CAT-002.4).</summary>
    public bool AllowBackorder { get; set; }

    /// <summary>Optional expected restock date shown while the variant is backordered (AC-CAT-013.3).</summary>
    public DateTime? EstimatedRestockDate { get; set; }

    /// <summary>Whether the variant is available for purchase (AC-CAT-002.4).</summary>
    public bool IsEnabled { get; set; } = true;

    public int DisplayOrder { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    /// <summary>The attribute-value combination that defines this variant.</summary>
    public ICollection<ProductVariantAttributeValue> AttributeValues { get; set; } = new List<ProductVariantAttributeValue>();
    public ICollection<TierPrice> TierPrices { get; set; } = new List<TierPrice>();
    public ICollection<InventoryLevel> InventoryLevels { get; set; } = new List<InventoryLevel>();
}
