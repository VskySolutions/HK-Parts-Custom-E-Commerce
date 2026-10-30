using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A node in the hierarchical, self-referencing category tree (REQ-CAT-004). Supports unlimited
/// nesting depth, per-node SEO metadata, display ordering, and an enable/disable toggle. A disabled
/// category (and its products) is excluded from all storefront responses.
/// </summary>
public class Category : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Parent node; null for a root category.</summary>
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }

    // SEO metadata (AC-CAT-004.3)
    public string? Slug { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Marks the category for the storefront "Featured Categories" showcase (WO-98).</summary>
    public bool IsFeatured { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
}
