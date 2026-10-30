using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductSearch;

/// <summary>A single product row in a storefront search result set (REQ-STF-002).</summary>
public class SearchResultItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ShortDescription { get; set; }

    /// <summary>The product's own price, or the lowest enabled variant price when the product has none.</summary>
    public decimal? Price { get; set; }
    public Guid? ManufacturerId { get; set; }

    /// <summary>Product type (Simple/Grouped/WithVariants/…); lets the storefront card route variation products to the detail page for option selection.</summary>
    public ProductType ProductType { get; set; }

    /// <summary>URL of the primary product-level image, or null when none is available.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Projects a product (with its <c>Pictures</c> and <c>Variants</c> loaded) into a search row.</summary>
    public static SearchResultItemDto From(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        ShortDescription = p.ShortDescription,
        Price = p.Price ?? p.Variants
            .Where(v => v.IsEnabled && v.Price.HasValue)
            .Min(v => v.Price),
        ManufacturerId = p.ManufacturerId,
        ProductType = p.ProductType,
        ImageUrl = p.Pictures
            .Where(i => i.ProductVariantId == null && i.Media != null && i.Media.MediaType == MediaType.Image)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.Media!.Url)
            .FirstOrDefault(),
    };
}

/// <summary>One possible value of a filterable specification facet plus its match count (AC-STF-003.4).</summary>
public class FacetValueDto
{
    public Guid OptionId { get; set; }
    public string Value { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>A filterable Specification Attribute present in the active product set and its values (AC-STF-003.1).</summary>
public class FacetDto
{
    public Guid SpecificationAttributeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<FacetValueDto> Values { get; set; } = new();
}

/// <summary>A page of search results together with pagination metadata and the applicable facets.</summary>
public class SearchResultsDto
{
    public List<SearchResultItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<FacetDto> Facets { get; set; } = new();
}

/// <summary>Autocomplete suggestions: matching product names and category names (AC-STF-002.2).</summary>
public class AutocompleteResultDto
{
    public List<string> Products { get; set; } = new();
    public List<string> Categories { get; set; } = new();
}
