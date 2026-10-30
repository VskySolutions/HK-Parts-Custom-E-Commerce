using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Assigns a centralized <see cref="Media"/> asset to a product as one of its gallery pictures (WO-123,
/// REQ-CAT-012). Replaces the ad-hoc URL-based <see cref="ProductImage"/> for admin-managed images: the
/// image bytes, SEO file name and alt text live on the Media record; this row only orders it on the product.
/// </summary>
public class ProductPicture : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Optional variant this media belongs to (AC-CAT-002.5); null = product-level.</summary>
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public Guid MediaId { get; set; }
    public Media? Media { get; set; }

    public int DisplayOrder { get; set; }
}
