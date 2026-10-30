using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// Returns a sortable page of published products carrying the given tag, resolved by slug or name
/// (AC-CAT-008.2). Throws <see cref="NotFoundException"/> when no such tag exists.
/// </summary>
public record GetProductsByTagQuery(
    string TagSlugOrName,
    int Page = 1,
    int PageSize = 20,
    string? Sort = null) : IRequest<PaginatedList<StorefrontProductSummaryDto>>;

public class GetProductsByTagQueryHandler : IRequestHandler<GetProductsByTagQuery, PaginatedList<StorefrontProductSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;

    public GetProductsByTagQueryHandler(IApplicationDbContext db, ICustomerGroupService groups)
    {
        _db = db;
        _groups = groups;
    }

    public async Task<PaginatedList<StorefrontProductSummaryDto>> Handle(GetProductsByTagQuery request, CancellationToken cancellationToken)
    {
        var key = request.TagSlugOrName?.Trim() ?? string.Empty;

        var tag = await _db.ProductTags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == key || t.Name == key, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductTag), key);

        var productsQuery = _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Published()
            .Where(p => p.Tags.Any(tm => tm.ProductTagId == tag.Id))
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
