using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsPageGroups;

/// <summary>An organising group for CMS pages (drives storefront footer/nav link columns).</summary>
public class CmsPageGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public int DisplayOrder { get; set; }

    public static CmsPageGroupDto From(CMSPageGroup g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        Slug = g.Slug,
        DisplayOrder = g.DisplayOrder,
    };
}
