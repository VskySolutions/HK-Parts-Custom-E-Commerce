using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Features.StorefrontCatalog;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsCategoryConfig;

/// <summary>
/// Full admin view of a category page's dynamic configuration (WO-99): the banner asset, promotional
/// description, "You May Also Like" collection and the ordered set of pinned products (each carrying the
/// product's name, SKU and primary image so the editor can render the ordered list without a second lookup).
/// One row per category; when a category has no config yet, <see cref="Empty"/> yields a blank shell so the
/// admin form can still render.
/// </summary>
public class CmsCategoryPageConfigDto
{
    public Guid CategoryId { get; set; }

    public Guid? BannerMediaId { get; set; }

    /// <summary>Resolved banner image URL (the banner Media asset's URL); null when no banner is set.</summary>
    public string? BannerImageUrl { get; set; }

    public string? PromotionalDescription { get; set; }

    public Guid? YmalCollectionId { get; set; }

    /// <summary>Display name of the selected "You May Also Like" collection; null when none is set.</summary>
    public string? YmalCollectionName { get; set; }

    /// <summary>The pinned products in display order. Rows whose product was soft-deleted are omitted (a product
    /// delete auto-removes it at read time).</summary>
    public List<CategoryPagePinnedProductDto> PinnedProducts { get; set; } = new();

    /// <summary>A blank configuration shell for a category that has no config row yet.</summary>
    public static CmsCategoryPageConfigDto Empty(Guid categoryId) => new() { CategoryId = categoryId };

    /// <summary>Projects the full config; expects the banner, YMAL collection and pinned products (with each
    /// product's product-level media) to be loaded via <see cref="CmsCategoryPageConfigQueries.WithDetails"/>.</summary>
    public static CmsCategoryPageConfigDto From(CMSCategoryPageConfig c) => new()
    {
        CategoryId = c.CategoryId,
        BannerMediaId = c.BannerMediaId,
        BannerImageUrl = c.BannerMedia?.Url,
        PromotionalDescription = c.PromotionalDescription,
        YmalCollectionId = c.YmalCollectionId,
        YmalCollectionName = c.YmalCollection?.Name,
        PinnedProducts = c.PinnedProducts
            .Where(p => p.Product != null)
            .OrderBy(p => p.DisplayOrder)
            .Select(CategoryPagePinnedProductDto.From)
            .ToList(),
    };
}

/// <summary>A single pinned product within a category page config, denormalised for the admin editor row.</summary>
public class CategoryPagePinnedProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }

    /// <summary>URL of the product's primary product-level image (image preferred over video thumbnail); null
    /// when the product has no product-level media. Resolved the same way as the storefront card.</summary>
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Projects a pinned row; expects <c>Product.Pictures.Media</c> to be loaded (and the product present).</summary>
    public static CategoryPagePinnedProductDto From(CMSCategoryPinnedProduct p) => new()
    {
        ProductId = p.ProductId,
        Name = p.Product!.Name,
        Sku = p.Product!.Sku,
        ImageUrl = p.Product!.Pictures
            .Where(pic => pic.ProductVariantId == null && pic.Media != null)
            .OrderBy(pic => pic.Media!.MediaType == MediaType.Image ? 0 : 1)
            .ThenBy(pic => pic.DisplayOrder)
            .Select(pic => pic.Media!.Url)
            .FirstOrDefault(),
        DisplayOrder = p.DisplayOrder,
    };
}

/// <summary>
/// Public storefront payload for a category page's dynamic configuration (WO-99). Consumed alongside the
/// existing category listing: the frontend injects <see cref="PinnedProducts"/> ahead of the regular grid
/// (pinned before unpinned regardless of sort) and renders the banner, promo copy and the YMAL row. An empty
/// shell (nulls / empty lists) is returned when a category has no config so the plain grid still renders.
/// </summary>
public class StorefrontCategoryConfigDto
{
    /// <summary>Resolved banner image URL; null when no banner is configured.</summary>
    public string? BannerImageUrl { get; set; }

    public string? PromotionalDescription { get; set; }

    /// <summary>Pinned products in display order, published/non-deleted only.</summary>
    public List<StorefrontProductSummaryDto> PinnedProducts { get; set; } = new();

    public Guid? YmalCollectionId { get; set; }

    /// <summary>"You May Also Like" collection products in curated order, published/non-deleted only.</summary>
    public List<StorefrontProductSummaryDto> YmalProducts { get; set; } = new();
}

/// <summary>Shared query composition for category page configs.</summary>
internal static class CmsCategoryPageConfigQueries
{
    /// <summary>Eager-loads a config's banner, YMAL collection and pinned products (with each product's
    /// product-level media), so a full <see cref="CmsCategoryPageConfigDto"/> can be projected in memory.</summary>
    public static IQueryable<CMSCategoryPageConfig> WithDetails(this IQueryable<CMSCategoryPageConfig> query) =>
        query
            .AsSplitQuery()
            .Include(c => c.BannerMedia)
            .Include(c => c.YmalCollection)
            .Include(c => c.PinnedProducts)
                .ThenInclude(p => p.Product)
                    .ThenInclude(prod => prod!.Pictures.Where(pic => pic.ProductVariantId == null))
                        .ThenInclude(pic => pic.Media);
}
