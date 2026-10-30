using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// Returns a sortable page of published products for an enabled manufacturer (resolved by id or slug)
/// (AC-CAT-005.3). Throws <see cref="NotFoundException"/> when the manufacturer is missing or disabled.
/// </summary>
public record BrowseByManufacturerQuery(
    string IdOrSlug,
    int Page = 1,
    int PageSize = 20,
    string? Sort = null) : IRequest<PaginatedList<StorefrontProductSummaryDto>>;

public class BrowseByManufacturerQueryHandler : IRequestHandler<BrowseByManufacturerQuery, PaginatedList<StorefrontProductSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;

    public BrowseByManufacturerQueryHandler(IApplicationDbContext db, ICustomerGroupService groups)
    {
        _db = db;
        _groups = groups;
    }

    public async Task<PaginatedList<StorefrontProductSummaryDto>> Handle(BrowseByManufacturerQuery request, CancellationToken cancellationToken)
    {
        var key = request.IdOrSlug?.Trim() ?? string.Empty;

        var enabled = _db.Manufacturers.AsNoTracking().Where(m => m.IsEnabled);
        var manufacturer = (Guid.TryParse(key, out var manufacturerId)
            ? await enabled.FirstOrDefaultAsync(m => m.Id == manufacturerId, cancellationToken)
            : await enabled.FirstOrDefaultAsync(m => m.Slug == key, cancellationToken))
            ?? throw new NotFoundException(nameof(Manufacturer), key);

        var productsQuery = _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Published()
            .Where(p => p.ManufacturerId == manufacturer.Id)
            .WithSummaryImages()
            .ApplyStorefrontSort(request.Sort);

        var page = await PaginatedList<Product>.CreateAsync(productsQuery, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(StorefrontProductSummaryDto.From).ToList();

        // A group member must see their price while browsing, not just in the cart (AC-CUS-003.5). Overlaid
        // on the projected page in one batch; a no-op for guests, so the anonymous path is unchanged.
        // Summaries are product-level cards, so there is no variant to key on.
        var groupId = await _groups.GetCurrentGroupIdAsync(cancellationToken);
        await _groups.ApplyGroupPricingAsync(
            items,
            groupId,
            i => i.Price is decimal price ? new GroupPriceRequest(i.Id, null, price) : null,
            (i, price) => i.Price = price,
            cancellationToken);

        return new PaginatedList<StorefrontProductSummaryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
