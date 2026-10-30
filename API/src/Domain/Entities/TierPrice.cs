using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A quantity-break pricing rule attached to a product or a specific variant (REQ-CAT-006).
/// Exactly one of <see cref="ProductId"/> / <see cref="ProductVariantId"/> is set.
/// </summary>
public class TierPrice : AuditableEntity
{
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    /// <summary>Minimum quantity at which <see cref="Price"/> applies (AC-CAT-006.1).</summary>
    public int MinQuantity { get; set; }

    /// <summary>Per-unit price for quantities at or above <see cref="MinQuantity"/>.</summary>
    public decimal Price { get; set; }
}
