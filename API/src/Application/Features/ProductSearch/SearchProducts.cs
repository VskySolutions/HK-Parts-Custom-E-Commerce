using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ProductSearch;

/// <summary>
/// Searches the published storefront catalog by keyword and filters, returning a sorted, paged result
/// set together with faceted-navigation counts (REQ-STF-002, REQ-STF-003).
/// </summary>
public record SearchProductsQuery(
    string? Query,
    List<Guid>? SpecificationOptionIds,
    decimal? MinPrice,
    decimal? MaxPrice,
    Guid? ManufacturerId,
    Guid? CategoryId,
    string? Sort,
    int Page = 1,
    int PageSize = 20) : IRequest<SearchResultsDto>;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchResultsDto>
{
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;

    public SearchProductsQueryHandler(IApplicationDbContext db, ICustomerGroupService groups)
    {
        _db = db;
        _groups = groups;
    }

    public async Task<SearchResultsDto> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, MaxPageSize);

        // Base storefront scope: published products only (REQ-STF-002/003), matching the sibling
        // StorefrontCatalog convention (StorefrontQueries.Published). Soft-deleted rows are already
        // excluded by global query filters; the enabled-category rule is enforced by the category
        // filter below (and category-page browsing), consistent with WO-17.
        IQueryable<Product> baseQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.IsPublished);

        // Keyword match across name, short/full description, or any assigned tag name.
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim();
            baseQuery = baseQuery.Where(p =>
                p.Name.Contains(term)
                || (p.ShortDescription != null && p.ShortDescription.Contains(term))
                || (p.FullDescription != null && p.FullDescription.Contains(term))
                || p.Tags.Any(t => t.ProductTag != null && t.ProductTag.Name.Contains(term)));
        }

        // Non-facet filters. These also constrain the facet counts computed below.
        if (request.ManufacturerId is { } manufacturerId)
            baseQuery = baseQuery.Where(p => p.ManufacturerId == manufacturerId);

        if (request.CategoryId is { } categoryId)
            baseQuery = baseQuery.Where(p =>
                p.ProductCategories.Any(pc => pc.CategoryId == categoryId && pc.Category!.IsEnabled));

        if (request.MinPrice is { } minPrice)
            baseQuery = baseQuery.Where(p => p.Price >= minPrice);

        if (request.MaxPrice is { } maxPrice)
            baseQuery = baseQuery.Where(p => p.Price <= maxPrice);

        // Facets reflect the query + non-facet filters but NOT the selected specification options, so
        // every available option keeps a visible count as selections are made (AC-STF-003.1/3.4).
        var facets = await ComputeFacetsAsync(baseQuery, cancellationToken);

        // The result set additionally requires the product to carry ALL selected options (AC-STF-003.2).
        IQueryable<Product> resultsQuery = baseQuery;
        if (request.SpecificationOptionIds is { Count: > 0 })
        {
            foreach (var optionId in request.SpecificationOptionIds.Where(id => id != Guid.Empty).Distinct())
            {
                var id = optionId;
                resultsQuery = resultsQuery.Where(p =>
                    p.SpecificationValues.Any(sv => sv.SpecificationAttributeOptionId == id));
            }
        }

        // Order, then eager-load only what the row projection needs: product-level pictures (for the
        // primary image) and enabled variants (for the min-price fallback). Filtered includes keep the
        // payload small; split query avoids a cartesian blow-up across the two collections under paging.
        var sorted = ApplySort(resultsQuery, request.Sort, request.Query)
            .Include(p => p.Pictures.Where(i => i.ProductVariantId == null)).ThenInclude(pic => pic.Media)
            .Include(p => p.Variants.Where(v => v.IsEnabled))
            .AsSplitQuery();

        var pageResult = await PaginatedList<Product>.CreateAsync(sorted, page, pageSize, cancellationToken);

        // A group member must see their price in search results, not just in the cart (AC-CUS-003.5).
        // Overlaid on the projected page in one batch; a no-op for guests, so the anonymous path is
        // unchanged. Rows are product-level cards, so there is no variant to key on.
        var items = pageResult.Items.Select(SearchResultItemDto.From).ToList();
        var groupId = await _groups.GetCurrentGroupIdAsync(cancellationToken);
        await _groups.ApplyGroupPricingAsync(
            items,
            groupId,
            i => i.Price is decimal price ? new GroupPriceRequest(i.Id, null, price) : null,
            (i, price) => i.Price = price,
            cancellationToken);

        return new SearchResultsDto
        {
            Items = items,
            TotalCount = pageResult.TotalCount,
            Page = pageResult.PageNumber,
            PageSize = pageResult.PageSize,
            Facets = facets,
        };
    }

    /// <summary>
    /// Builds the facet list for the supplied (query + non-facet-filtered) product set: for every
    /// filterable specification option present, the count of matching products. The candidate rows are
    /// loaded once (via an <c>IN (&lt;product-id subquery&gt;)</c>) then grouped in memory, which keeps
    /// the SQL to a single flat join and avoids fragile GROUP-BY-over-navigation translation.
    /// </summary>
    private async Task<List<FacetDto>> ComputeFacetsAsync(IQueryable<Product> facetBase, CancellationToken cancellationToken)
    {
        var productIds = facetBase.Select(p => p.Id);

        var rows = await _db.ProductSpecificationValues
            .AsNoTracking()
            .Where(sv => productIds.Contains(sv.ProductId)
                         && sv.SpecificationAttributeOption!.SpecificationAttribute!.IsFilterable)
            .Select(sv => new
            {
                sv.ProductId,
                AttributeId = sv.SpecificationAttributeOption!.SpecificationAttributeId,
                AttributeName = sv.SpecificationAttributeOption!.SpecificationAttribute!.Name,
                AttributeOrder = sv.SpecificationAttributeOption!.SpecificationAttribute!.DisplayOrder,
                OptionId = sv.SpecificationAttributeOptionId,
                OptionValue = sv.SpecificationAttributeOption!.Value,
                OptionOrder = sv.SpecificationAttributeOption!.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.AttributeId, r.AttributeName, r.AttributeOrder })
            .OrderBy(g => g.Key.AttributeOrder)
            .ThenBy(g => g.Key.AttributeName)
            .Select(attr => new FacetDto
            {
                SpecificationAttributeId = attr.Key.AttributeId,
                Name = attr.Key.AttributeName,
                Values = attr
                    .GroupBy(r => new { r.OptionId, r.OptionValue, r.OptionOrder })
                    .OrderBy(o => o.Key.OptionOrder)
                    .ThenBy(o => o.Key.OptionValue)
                    .Select(o => new FacetValueDto
                    {
                        OptionId = o.Key.OptionId,
                        Value = o.Key.OptionValue,
                        Count = o.Select(x => x.ProductId).Distinct().Count(),
                    })
                    .ToList(),
            })
            .ToList();
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> query, string? sort, string? searchTerm)
    {
        switch (sort?.Trim().ToLowerInvariant())
        {
            case "price_asc":
                // Push null prices last (they sort first under a raw ascending ORDER BY in SQL Server).
                return query.OrderBy(p => p.Price == null).ThenBy(p => p.Price).ThenBy(p => p.Name).ThenBy(p => p.Id);
            case "price_desc":
                return query.OrderByDescending(p => p.Price).ThenBy(p => p.Name).ThenBy(p => p.Id);
            case "name_asc":
                return query.OrderBy(p => p.Name).ThenBy(p => p.Id);
            case "name_desc":
                return query.OrderByDescending(p => p.Name).ThenBy(p => p.Id);
            case "newest":
                return query.OrderByDescending(p => p.CreatedOnUtc).ThenBy(p => p.Id);
            default:
                // "relevance" (and unknown/blank): products whose name starts with the query rank first.
                var term = searchTerm?.Trim();
                if (!string.IsNullOrEmpty(term))
                    return query
                        .OrderByDescending(p => p.Name.StartsWith(term))
                        .ThenBy(p => p.Name)
                        .ThenBy(p => p.Id);
                return query.OrderBy(p => p.Name).ThenBy(p => p.Id);
        }
    }
}
