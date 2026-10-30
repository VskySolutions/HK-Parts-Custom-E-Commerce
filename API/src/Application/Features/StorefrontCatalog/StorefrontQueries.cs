using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// Shared query composition for the public storefront catalog: the published-only restriction,
/// primary-image eager-loading, and the listing sort used by the category/tag/manufacturer pages.
/// Soft-deleted rows are already excluded by the entities' global query filters.
/// </summary>
internal static class StorefrontQueries
{
    /// <summary>Restricts to published products (soft-deleted rows are already filtered out globally).</summary>
    public static IQueryable<Product> Published(this IQueryable<Product> query) =>
        query.Where(p => p.IsPublished);

    /// <summary>Eager-loads the product-level media required to resolve a summary's primary image.</summary>
    public static IQueryable<Product> WithSummaryImages(this IQueryable<Product> query) =>
        query.Include(p => p.Pictures.Where(i => i.ProductVariantId == null)).ThenInclude(pic => pic.Media);

    /// <summary>
    /// Applies the storefront listing sort. Supported keys: <c>price</c>/<c>price_desc</c>,
    /// <c>name</c>/<c>name_desc</c>, <c>newest</c>/<c>oldest</c>. An empty or unrecognised value
    /// falls back to the curated order (display order, then name).
    /// </summary>
    public static IQueryable<Product> ApplyStorefrontSort(this IQueryable<Product> query, string? sort) =>
        (sort?.Trim().ToLowerInvariant()) switch
        {
            "price" or "price_asc" => query.OrderBy(p => p.Price == null).ThenBy(p => p.Price).ThenBy(p => p.Name),
            "price_desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Name),
            "name" or "name_asc" => query.OrderBy(p => p.Name),
            "name_desc" => query.OrderByDescending(p => p.Name),
            "newest" => query.OrderByDescending(p => p.CreatedOnUtc).ThenBy(p => p.Name),
            "oldest" => query.OrderBy(p => p.CreatedOnUtc).ThenBy(p => p.Name),
            _ => query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name),
        };
}
