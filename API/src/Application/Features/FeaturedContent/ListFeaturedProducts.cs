using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.FeaturedContent;

/// <summary>
/// Lists every product flagged featured, in curated storefront order (<c>FeaturedDisplayOrder</c> then
/// name), for the admin manage-featured screen (WO-98). Unlike the storefront resolver this is <b>not</b>
/// restricted to published products — an admin must be able to see and reorder a featured item that is
/// currently unpublished (its <c>IsPublished</c> is surfaced on each row).
/// </summary>
public record ListFeaturedProductsQuery() : IRequest<IReadOnlyList<FeaturedProductDto>>;

public class ListFeaturedProductsQueryHandler : IRequestHandler<ListFeaturedProductsQuery, IReadOnlyList<FeaturedProductDto>>
{
    private readonly IApplicationDbContext _db;

    public ListFeaturedProductsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeaturedProductDto>> Handle(ListFeaturedProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Where(p => p.IsFeatured)
            .Include(p => p.Pictures.Where(i => i.ProductVariantId == null))
                .ThenInclude(pic => pic.Media)
            .OrderBy(p => p.FeaturedDisplayOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return products.Select(FeaturedProductDto.From).ToList();
    }
}
