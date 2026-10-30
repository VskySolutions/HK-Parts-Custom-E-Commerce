using System.Linq.Expressions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.FeaturedContent;

/// <summary>
/// Admin row for a featured product: the manage-featured list and the set/reorder responses (WO-98).
/// Carries the primary image plus <see cref="IsPublished"/> so an admin can tell whether a featured item
/// is actually visible on the storefront (the storefront resolver shows published featured products only).
/// </summary>
public class FeaturedProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal? Price { get; set; }

    /// <summary>Primary product-level image URL (an image is preferred over a video thumbnail); null when the product has no product-level media. Requires <see cref="Product.Pictures"/> (with Media) loaded.</summary>
    public string? ImageUrl { get; set; }

    public int FeaturedDisplayOrder { get; set; }

    /// <summary>Whether the product is published; a featured-but-unpublished product is hidden from the storefront row.</summary>
    public bool IsPublished { get; set; }

    /// <summary>Projects an admin featured-product row; expects the product-level <see cref="Product.Pictures"/> (with Media) to be loaded.</summary>
    public static FeaturedProductDto From(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Sku = p.Sku,
        Price = p.Price,
        ImageUrl = p.Pictures
            .Where(i => i.ProductVariantId == null && i.Media != null)
            .OrderBy(i => i.Media!.MediaType == MediaType.Image ? 0 : 1)
            .ThenBy(i => i.DisplayOrder)
            .Select(i => i.Media!.Url)
            .FirstOrDefault(),
        FeaturedDisplayOrder = p.FeaturedDisplayOrder,
        IsPublished = p.IsPublished,
    };
}

/// <summary>
/// Admin row for a featured category: the "Featured Categories" showcase management list (WO-98). A small
/// summary — id/name/slug/image/order — since the storefront category tree DTO
/// (<c>StorefrontCategoryNodeDto</c>) carries product counts and nested children rather than a flat image card.
/// </summary>
public class FeaturedCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }

    /// <summary>Primary category picture URL, or null when the category has no picture.</summary>
    public string? ImageUrl { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>
    /// EF projection resolving each category plus its primary picture URL. The image lives on
    /// <see cref="CategoryPicture"/> (Category has no picture navigation), so the correlated subquery reads
    /// it from the shared context, using the denormalized <see cref="Media.Url"/> exactly like the
    /// storefront product cards. Shared by the list query and the set-featured response for a consistent shape.
    /// </summary>
    public static Expression<Func<Category, FeaturedCategoryDto>> Projection(IApplicationDbContext db) =>
        c => new FeaturedCategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            DisplayOrder = c.DisplayOrder,
            ImageUrl = db.CategoryPictures
                .Where(cp => cp.CategoryId == c.Id && cp.Media != null)
                .OrderBy(cp => cp.DisplayOrder)
                .Select(cp => cp.Media!.Url)
                .FirstOrDefault(),
        };
}
