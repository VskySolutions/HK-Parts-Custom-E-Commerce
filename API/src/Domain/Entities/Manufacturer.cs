using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A brand/maker record associable to products and filterable on the storefront (REQ-CAT-005).
/// </summary>
public class Manufacturer : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Central Media asset for the logo (preferred). Uploads set this going forward.</summary>
    public Guid? LogoMediaId { get; set; }
    public Media? LogoMedia { get; set; }

    /// <summary>Legacy logo URL, retained as a read fallback for records migrated before <see cref="LogoMediaId"/>.</summary>
    public string? LogoUrl { get; set; }

    // SEO metadata (AC-CAT-005.1)
    public string? Slug { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
