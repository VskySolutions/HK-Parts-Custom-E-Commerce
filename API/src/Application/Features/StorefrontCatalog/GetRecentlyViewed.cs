using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// Returns summaries for the client-supplied recently-viewed product ids (AC-STF-005.1). The
/// recently-viewed list is maintained client/session side; this only resolves ids to published
/// products, preserving the caller's order and dropping unknown, unpublished or duplicate ids.
/// </summary>
public record GetRecentlyViewedQuery(List<Guid> ProductIds) : IRequest<IReadOnlyList<StorefrontProductSummaryDto>>;

public class GetRecentlyViewedQueryHandler : IRequestHandler<GetRecentlyViewedQuery, IReadOnlyList<StorefrontProductSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;

    public GetRecentlyViewedQueryHandler(IApplicationDbContext db, ICustomerGroupService groups)
    {
        _db = db;
        _groups = groups;
    }

    public async Task<IReadOnlyList<StorefrontProductSummaryDto>> Handle(GetRecentlyViewedQuery request, CancellationToken cancellationToken)
    {
        var ids = (request.ProductIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .ToList();

        if (ids.Count == 0)
            return Array.Empty<StorefrontProductSummaryDto>();

        var distinctIds = ids.Distinct().ToList();

        var products = await _db.Products
            .AsNoTracking()
            .Published()
            .Where(p => distinctIds.Contains(p.Id))
            .WithSummaryImages()
            .ToListAsync(cancellationToken);

        var byId = products.ToDictionary(p => p.Id, StorefrontProductSummaryDto.From);

        // Preserve the caller's order and de-duplicate, keeping the first occurrence of each id.
        var result = new List<StorefrontProductSummaryDto>();
        var seen = new HashSet<Guid>();
        foreach (var id in ids)
        {
            if (seen.Add(id) && byId.TryGetValue(id, out var dto))
                result.Add(dto);
        }

        // A group member must see their price while browsing, not just in the cart (AC-CUS-003.5). Overlaid
        // on the de-duplicated result in one batch; a no-op for guests, so the anonymous path is unchanged.
        // Summaries are product-level cards, so there is no variant to key on.
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
