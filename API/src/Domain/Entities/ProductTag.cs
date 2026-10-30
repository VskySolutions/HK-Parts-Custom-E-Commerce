using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A free-form tag assignable to products for tag-based browsing (REQ-CAT-008).</summary>
public class ProductTag : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<ProductTagMapping> ProductTags { get; set; } = new List<ProductTagMapping>();
}

/// <summary>Join between <see cref="Product"/> and <see cref="ProductTag"/> (many-to-many).</summary>
public class ProductTagMapping : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid ProductTagId { get; set; }
    public ProductTag? ProductTag { get; set; }
}
