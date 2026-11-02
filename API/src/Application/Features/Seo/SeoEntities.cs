using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>
/// Per-entity-type facts the central SEO feature needs: which admin module owns the type (for access
/// checks), whether a record exists, and the storefront path prefix used to build absolute URLs. Adding
/// SEO to a new module = a new <see cref="SeoEntityType"/> member + one case in each switch below.
/// </summary>
public static class SeoEntities
{
    /// <summary>The admin module whose access gates editing this entity type's SEO.</summary>
    public static string ModuleFor(SeoEntityType type) => type switch
    {
        SeoEntityType.Product or SeoEntityType.Category or SeoEntityType.Manufacturer or SeoEntityType.ProductTag
            => Modules.Catalog,
        SeoEntityType.CmsPage or SeoEntityType.BlogPost or SeoEntityType.ProductCollection
            => Modules.Cms,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported SEO entity type."),
    };

    /// <summary>
    /// Storefront path prefix (relative to the storefront base URL) that the slug is appended to, matching
    /// WEB <c>modules/storefront/routes.js</c>. Null when the type has no storefront page yet.
    /// </summary>
    public static string? StorefrontPathFor(SeoEntityType type) => type switch
    {
        SeoEntityType.Product => "/shop/product/",
        SeoEntityType.Category => "/shop/category/",
        SeoEntityType.CmsPage => "/shop/page/",
        SeoEntityType.BlogPost => "/shop/blog/",
        _ => null,
    };

    /// <summary>Whether a (non-soft-deleted) record of <paramref name="type"/> with this id exists.</summary>
    public static Task<bool> ExistsAsync(IApplicationDbContext db, SeoEntityType type, Guid id, CancellationToken ct) => type switch
    {
        SeoEntityType.Product => db.Products.AnyAsync(x => x.Id == id, ct),
        SeoEntityType.Category => db.Categories.AnyAsync(x => x.Id == id, ct),
        SeoEntityType.Manufacturer => db.Manufacturers.AnyAsync(x => x.Id == id, ct),
        SeoEntityType.CmsPage => db.CMSPages.AnyAsync(x => x.Id == id, ct),
        SeoEntityType.BlogPost => db.CMSBlogPosts.AnyAsync(x => x.Id == id, ct),
        SeoEntityType.ProductTag => db.ProductTags.AnyAsync(x => x.Id == id, ct),
        SeoEntityType.ProductCollection => db.CMSProductCollections.AnyAsync(x => x.Id == id, ct),
        _ => Task.FromResult(false),
    };

    /// <summary>Loads the storefront SEO payload for a record (empty payload when no row exists).</summary>
    public static async Task<StorefrontSeoDto> GetStorefrontSeoAsync(
        IApplicationDbContext db, SeoEntityType type, Guid id, CancellationToken ct)
    {
        var row = await db.SeoMetadata
            .AsNoTracking()
            .Include(s => s.MetaImage)
            .FirstOrDefaultAsync(s => s.EntityType == type && s.EntityId == id, ct);
        return StorefrontSeoDto.From(row);
    }
}
