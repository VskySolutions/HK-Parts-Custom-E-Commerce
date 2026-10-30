using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>
/// Full admin view of an admin-curated product collection (WO-97): its metadata plus its ordered items,
/// each carrying the product's name, SKU and primary image so the editor can render the ordered list
/// without a second lookup per row.
/// </summary>
public class CmsProductCollectionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Slug { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>The collection's products in display order. Items whose product was soft-deleted are omitted
    /// (a product delete auto-removes it from every collection at read time).</summary>
    public List<CmsProductCollectionItemDto> Items { get; set; } = new();

    /// <summary>Projects the full collection; expects the items (with each product's product-level media) to be
    /// loaded via <see cref="CmsProductCollectionQueries.WithItemsAndMedia"/>.</summary>
    public static CmsProductCollectionDto From(CMSProductCollection c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        Slug = c.Slug,
        IsEnabled = c.IsEnabled,
        CreatedOnUtc = c.CreatedOnUtc,
        UpdatedOnUtc = c.UpdatedOnUtc,
        Items = c.Items
            .Where(i => i.Product != null)
            .OrderBy(i => i.DisplayOrder)
            .Select(CmsProductCollectionItemDto.From)
            .ToList(),
    };
}

/// <summary>A single ordered product within a collection, denormalised for the admin editor row.</summary>
public class CmsProductCollectionItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }

    /// <summary>URL of the product's primary product-level image (image preferred over video thumbnail); null
    /// when the product has no product-level media. Resolved the same way as the storefront card.</summary>
    public string? PrimaryImageUrl { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Projects an item; expects <c>Product.Pictures.Media</c> to be loaded (and the product present).</summary>
    public static CmsProductCollectionItemDto From(CMSProductCollectionItem i) => new()
    {
        ProductId = i.ProductId,
        ProductName = i.Product!.Name,
        Sku = i.Product!.Sku,
        PrimaryImageUrl = i.Product!.Pictures
            .Where(pic => pic.ProductVariantId == null && pic.Media != null)
            .OrderBy(pic => pic.Media!.MediaType == MediaType.Image ? 0 : 1)
            .ThenBy(pic => pic.DisplayOrder)
            .Select(pic => pic.Media!.Url)
            .FirstOrDefault(),
        DisplayOrder = i.DisplayOrder,
    };
}

/// <summary>Admin list row for a product collection: identity, enabled state, its product count and when it
/// last changed (item add/remove/reorder bumps this too).</summary>
public class CmsProductCollectionListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public bool IsEnabled { get; set; }
    public int ProductCount { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    // No static From: the list handler projects this in SQL so ProductCount is a COUNT sub-query rather than
    // materialising every item row just to count it.
}

/// <summary>Shared query composition for product collections.</summary>
internal static class CmsProductCollectionQueries
{
    /// <summary>Eager-loads a collection's items with each product's product-level media, so a full
    /// <see cref="CmsProductCollectionDto"/> (name / SKU / primary image per row) can be projected in memory.</summary>
    public static IQueryable<CMSProductCollection> WithItemsAndMedia(this IQueryable<CMSProductCollection> query) =>
        query
            .AsSplitQuery()
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p!.Pictures.Where(pic => pic.ProductVariantId == null))
                        .ThenInclude(pic => pic.Media);
}
