using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Shared query composition for loading a product together with its full child graph.</summary>
internal static class ProductQueries
{
    /// <summary>
    /// Eager-loads every collection required to project a complete <see cref="ProductDto"/>. Combine
    /// with <c>AsSplitQuery()</c> to avoid a large cartesian join across the many collections.
    /// </summary>
    public static IQueryable<Product> WithFullGraph(this IQueryable<Product> query) =>
        query
            .Include(p => p.InventoryLevels)
            .Include(p => p.Variants).ThenInclude(v => v.InventoryLevels)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues)
            .Include(p => p.Variants).ThenInclude(v => v.TierPrices)
            .Include(p => p.Pictures).ThenInclude(pic => pic.Media)
            .Include(p => p.TierPrices)
            .Include(p => p.ProductCategories)
            .Include(p => p.Tags).ThenInclude(t => t.ProductTag)
            .Include(p => p.AttributeMappings)
            .Include(p => p.SpecificationValues)
            .Include(p => p.Relationships)
            .Include(p => p.GroupedMembers)
            .Include(p => p.Downloads);
}
