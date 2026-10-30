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

    /// <summary>Page heading override; the storefront falls back to <see cref="Name"/>.</summary>
    public string? H1 { get; set; }
    public Guid? MetaImageMediaId { get; set; }
    public Media? MetaImage { get; set; }
    public string? MetaOpenGraph { get; set; }
    public string? MetaCustomTags { get; set; }
    public string? MetaFooterTags { get; set; }

    /// <summary>Short plain-text teaser shown on category tiles / listings.</summary>
    public string? Summary { get; set; }

    /// <summary>Whether the category appears in the storefront navigation menu.</summary>
    public bool ShowInMenu { get; set; } = true;

    // Two fixed image slots (e.g. listing tile + page banner), each with its own alt text.
    public Guid? ImageMediaId { get; set; }
    public Media? Image { get; set; }
    public string? ImageAltText { get; set; }
    public Guid? Image2MediaId { get; set; }
    public Media? Image2 { get; set; }
    public string? Image2AltText { get; set; }

    /// <summary>Offer the brand (manufacturer) facet on this category's listing.</summary>
    public bool ShowBrandFilter { get; set; }
    /// <summary>Specification attributes offered as facets on this category's listing.</summary>
    public ICollection<CategoryFilterAttribute> FilterAttributes { get; set; } = new List<CategoryFilterAttribute>();

    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Marks the category for the storefront "Featured Categories" showcase (WO-98).</summary>
    public bool IsFeatured { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
}
