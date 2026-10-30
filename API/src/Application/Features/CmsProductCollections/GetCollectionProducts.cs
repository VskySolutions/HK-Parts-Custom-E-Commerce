using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.StorefrontCatalog;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>
/// Public storefront read for a product collection (WO-97): its products, in the admin-curated order, as the
/// same <see cref="StorefrontProductSummaryDto"/> cards used everywhere else on the storefront. Only products
/// that are published (and not soft-deleted) are returned — this is how a product delete is honoured as an
/// auto-removal from every collection at read time. A disabled or deleted collection yields an empty list.
/// </summary>
public record GetCollectionProductsQuery(Guid Id) : IRequest<IReadOnlyList<StorefrontProductSummaryDto>>;

public class GetCollectionProductsQueryHandler : IRequestHandler<GetCollectionProductsQuery, IReadOnlyList<StorefrontProductSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;

    public GetCollectionProductsQueryHandler(IApplicationDbContext db, ICustomerGroupService groups)
    {
        _db = db;
        _groups = groups;
    }

    public async Task<IReadOnlyList<StorefrontProductSummaryDto>> Handle(GetCollectionProductsQuery request, CancellationToken cancellationToken)
    {
        // Skip a disabled collection (a deleted one is already excluded by its global query filter) → empty list.
        var isEnabled = await _db.CMSProductCollections
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.Id && c.IsEnabled, cancellationToken);
        if (!isEnabled)
            return Array.Empty<StorefrontProductSummaryDto>();

        // Only items whose product is present (not soft-deleted; the Product query filter handles that) and
        // published survive, in curated order. Mirror the storefront listing's product-level media include so
        // the summary's primary image resolves identically.
        var items = await _db.CMSProductCollectionItems
            .AsNoTracking()
            .AsSplitQuery()
            .Where(i => i.CollectionId == request.Id && i.Product != null && i.Product.IsPublished)
            .OrderBy(i => i.DisplayOrder)
            .Include(i => i.Product)
                .ThenInclude(p => p!.Pictures.Where(pic => pic.ProductVariantId == null))
                    .ThenInclude(pic => pic.Media)
            .ToListAsync(cancellationToken);

        var result = items.Select(i => StorefrontProductSummaryDto.From(i.Product!)).ToList();

        // Overlay Customer Group pricing so a group member sees their price on collection cards too, exactly as
        // on every other storefront listing surface (AC-CUS-003.5). A no-op for guests, so anonymous is unchanged.
        var groupId = await _groups.GetCurrentGroupIdAsync(cancellationToken);
        await _groups.ApplyGroupPricingAsync(
            result,
            groupId,
            i => i.Price is decimal price ? new GroupPriceRequest(i.Id, null, price) : null,
            (i, price) => i.Price = price,
            cancellationToken);

        return result;
    }
}
