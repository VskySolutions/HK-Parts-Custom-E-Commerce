using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>Full view of a catalog category node.</summary>
public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public string? Slug { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; }

    public static CategoryDto From(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        ParentId = c.ParentId,
        Slug = c.Slug,
        MetaTitle = c.MetaTitle,
        MetaDescription = c.MetaDescription,
        MetaKeywords = c.MetaKeywords,
        CanonicalUrl = c.CanonicalUrl,
        DisplayOrder = c.DisplayOrder,
        IsEnabled = c.IsEnabled,
    };
}

/// <summary>A category node plus its ordered descendants, for rendering the full category tree.</summary>
public class CategoryTreeNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public string? Slug { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; }
    public List<CategoryTreeNodeDto> Children { get; set; } = new();

    public static CategoryTreeNodeDto From(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        ParentId = c.ParentId,
        Slug = c.Slug,
        MetaTitle = c.MetaTitle,
        MetaDescription = c.MetaDescription,
        MetaKeywords = c.MetaKeywords,
        CanonicalUrl = c.CanonicalUrl,
        DisplayOrder = c.DisplayOrder,
        IsEnabled = c.IsEnabled,
    };
}
