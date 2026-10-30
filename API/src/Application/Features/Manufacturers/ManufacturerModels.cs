using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Manufacturers;

/// <summary>Full view of a manufacturer/brand.</summary>
public class ManufacturerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Central Media asset id for the logo (preferred going forward; null for un-migrated legacy records).</summary>
    public Guid? LogoMediaId { get; set; }

    /// <summary>Resolved logo URL: the Media asset's URL when present, else the legacy fallback column.</summary>
    public string? LogoUrl { get; set; }
    public string? Slug { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; }

    public static ManufacturerDto From(Manufacturer m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        LogoMediaId = m.LogoMediaId,
        LogoUrl = m.LogoMedia?.Url ?? m.LogoUrl,
        Slug = m.Slug,
        MetaTitle = m.MetaTitle,
        MetaDescription = m.MetaDescription,
        MetaKeywords = m.MetaKeywords,
        DisplayOrder = m.DisplayOrder,
        IsEnabled = m.IsEnabled,
    };
}
