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
    public string? Summary { get; set; }
    public bool ShowInMenu { get; set; }
    public Guid? ImageMediaId { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageAltText { get; set; }
    public Guid? Image2MediaId { get; set; }
    public string? Image2Url { get; set; }
    public string? Image2AltText { get; set; }
    public string? H1 { get; set; }
    public Guid? MetaImageMediaId { get; set; }
    public string? MetaImageUrl { get; set; }
    public string? MetaOpenGraph { get; set; }
    public string? MetaCustomTags { get; set; }
    public string? MetaFooterTags { get; set; }
    public bool ShowBrandFilter { get; set; }
    /// <summary>Specification attributes offered as facets, in display order.</summary>
    public List<Guid> FilterSpecificationAttributeIds { get; set; } = new();

    /// <summary>Projects a category; image URLs and filters need the media / FilterAttributes loaded (see <c>CategoryQueries.WithDetails</c>).</summary>
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
        Summary = c.Summary,
        ShowInMenu = c.ShowInMenu,
        ImageMediaId = c.ImageMediaId,
        ImageUrl = c.Image?.Url,
        ImageAltText = c.ImageAltText,
        Image2MediaId = c.Image2MediaId,
        Image2Url = c.Image2?.Url,
        Image2AltText = c.Image2AltText,
        H1 = c.H1,
        MetaImageMediaId = c.MetaImageMediaId,
        MetaImageUrl = c.MetaImage?.Url,
        MetaOpenGraph = c.MetaOpenGraph,
        MetaCustomTags = c.MetaCustomTags,
        MetaFooterTags = c.MetaFooterTags,
        ShowBrandFilter = c.ShowBrandFilter,
        FilterSpecificationAttributeIds = c.FilterAttributes
            .OrderBy(f => f.DisplayOrder)
            .Select(f => f.SpecificationAttributeId)
            .ToList(),
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
    public bool ShowInMenu { get; set; }
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
        ShowInMenu = c.ShowInMenu,
    };
}
