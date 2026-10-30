using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// The public storefront category tree (enabled categories only) with a published-product count per
/// category — powers the header mega-menu and the home-page category grid. Disabled categories and their
/// descendants are pruned. No auth: this is buyer-facing navigation (mirrors the catalog controller).
/// </summary>
public record GetStorefrontCategoriesQuery() : IRequest<List<StorefrontCategoryNodeDto>>;

public class GetStorefrontCategoriesQueryHandler : IRequestHandler<GetStorefrontCategoriesQuery, List<StorefrontCategoryNodeDto>>
{
    private readonly IApplicationDbContext _db;

    public GetStorefrontCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<StorefrontCategoryNodeDto>> Handle(GetStorefrontCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new StorefrontCategoryNodeDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ParentId = c.ParentId,
                ProductCount = _db.Products.Count(p =>
                    p.IsPublished && p.ProductCategories.Any(pc => pc.CategoryId == c.Id)),
            })
            .ToListAsync(cancellationToken);

        // Assemble the tree, keeping only subtrees reachable from an enabled root (a category whose parent
        // is disabled/absent becomes a root so it is never orphaned out of the menu).
        var byId = categories.ToDictionary(c => c.Id);
        var roots = new List<StorefrontCategoryNodeDto>();

        foreach (var node in categories)
        {
            if (node.ParentId.HasValue && byId.TryGetValue(node.ParentId.Value, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        return roots;
    }
}
