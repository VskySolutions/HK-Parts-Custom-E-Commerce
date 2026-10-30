using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.FeaturedContent;

/// <summary>
/// Lists the categories flagged for the storefront "Featured Categories" showcase, in curated order
/// (<c>DisplayOrder</c> then name), for the admin management screen (WO-98). Not restricted to enabled
/// categories so an admin can manage a featured-but-disabled category; each row carries its primary image.
/// </summary>
public record ListFeaturedCategoriesQuery() : IRequest<IReadOnlyList<FeaturedCategoryDto>>;

public class ListFeaturedCategoriesQueryHandler : IRequestHandler<ListFeaturedCategoriesQuery, IReadOnlyList<FeaturedCategoryDto>>
{
    private readonly IApplicationDbContext _db;

    public ListFeaturedCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeaturedCategoryDto>> Handle(ListFeaturedCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsFeatured)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(FeaturedCategoryDto.Projection(_db))
            .ToListAsync(cancellationToken);
    }
}
