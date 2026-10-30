using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.CmsProductCollections;
using HKPartsEcommerce.Application.Features.StorefrontCatalog;

namespace HKPartsEcommerce.Application.Features.CmsCategoryConfig;

/// <summary>
/// Public storefront read for a category's dynamic page configuration (WO-99): the banner image, promotional
/// description, the published pinned products (in curated order) and the published products of the "You May
/// Also Like" collection. This is a SEPARATE endpoint the storefront calls alongside the regular category
/// listing — the frontend injects the pinned products ahead of the grid client-side (so pinned come before
/// unpinned regardless of the shopper's chosen sort). A category with no config yields an empty payload
/// (nulls / empty lists), never a 404, so the plain product grid still renders.
/// </summary>
public record GetStorefrontCategoryConfigQuery(Guid CategoryId) : IRequest<StorefrontCategoryConfigDto>;

public class GetStorefrontCategoryConfigQueryHandler : IRequestHandler<GetStorefrontCategoryConfigQuery, StorefrontCategoryConfigDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;
    private readonly ISender _mediator;

    public GetStorefrontCategoryConfigQueryHandler(IApplicationDbContext db, ICustomerGroupService groups, ISender mediator)
    {
        _db = db;
        _groups = groups;
        _mediator = mediator;
    }

    public async Task<StorefrontCategoryConfigDto> Handle(GetStorefrontCategoryConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _db.CMSCategoryPageConfigs
            .AsNoTracking()
            .Include(c => c.BannerMedia)
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId, cancellationToken);

        // No config → empty shell so the storefront just renders its plain grid.
        if (config is null)
            return new StorefrontCategoryConfigDto();

        // Pinned products: only those whose product is present (not soft-deleted; the Product query filter
        // handles that) and published survive, in curated order. Mirror the storefront listing's product-level
        // media include so the summary's primary image resolves identically.
        var pinnedRows = await _db.CMSCategoryPinnedProducts
            .AsNoTracking()
            .AsSplitQuery()
            .Where(p => p.CategoryPageConfigId == config.Id && p.Product != null && p.Product.IsPublished)
            .OrderBy(p => p.DisplayOrder)
            .Include(p => p.Product)
                .ThenInclude(prod => prod!.Pictures.Where(pic => pic.ProductVariantId == null))
                    .ThenInclude(pic => pic.Media)
            .ToListAsync(cancellationToken);

        var pinned = pinnedRows.Select(p => StorefrontProductSummaryDto.From(p.Product!)).ToList();

        // Overlay Customer Group pricing so a group member sees their price on the pinned cards too, exactly as
        // on every other storefront listing surface (AC-CUS-003.5). A no-op for guests.
        var groupId = await _groups.GetCurrentGroupIdAsync(cancellationToken);
        await _groups.ApplyGroupPricingAsync(
            pinned,
            groupId,
            i => i.Price is decimal price ? new GroupPriceRequest(i.Id, null, price) : null,
            (i, price) => i.Price = price,
            cancellationToken);

        // YMAL products: reuse the collection read verbatim — published-only, curated order, group pricing and
        // the disabled/deleted-collection → empty-list behaviour all come for free.
        var ymalProducts = config.YmalCollectionId is Guid ymalId
            ? await _mediator.Send(new GetCollectionProductsQuery(ymalId), cancellationToken)
            : (IReadOnlyList<StorefrontProductSummaryDto>)Array.Empty<StorefrontProductSummaryDto>();

        return new StorefrontCategoryConfigDto
        {
            BannerImageUrl = config.BannerMedia?.Url,
            PromotionalDescription = config.PromotionalDescription,
            PinnedProducts = pinned,
            YmalCollectionId = config.YmalCollectionId,
            YmalProducts = ymalProducts.ToList(),
        };
    }
}
