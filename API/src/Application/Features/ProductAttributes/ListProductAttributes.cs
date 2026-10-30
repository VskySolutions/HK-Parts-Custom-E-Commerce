using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductAttributes;

/// <summary>Returns a page of product attributes ordered by display order then name, optionally filtered by a name search term and/or display type.</summary>
public record ListProductAttributesQuery(int Page = 1, int PageSize = 20, string? Search = null, ProductAttributeDisplayType? DisplayType = null, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<ProductAttributeDto>>;

public class ListProductAttributesQueryHandler : IRequestHandler<ListProductAttributesQuery, PaginatedList<ProductAttributeDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    // (valuesCount/inUseCount are aggregates, not mapped columns, so they aren't sortable server-side.)
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["displayType"] = "DisplayType",
    };

    private readonly IApplicationDbContext _db;

    public ListProductAttributesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ProductAttributeDto>> Handle(ListProductAttributesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ProductAttribute> query = _db.ProductAttributes
            .AsNoTracking()
            .Include(a => a.Values);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a => a.Name.Contains(term));
        }

        if (request.DisplayType.HasValue)
            query = query.Where(a => a.DisplayType == request.DisplayType.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Name));

        var page = await PaginatedList<ProductAttribute>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ProductAttributeDto.From).ToList();

        // Populate the "In use" product count per attribute (drives the list column + delete protection).
        var ids = items.Select(i => i.Id).ToList();
        if (ids.Count > 0)
        {
            var usage = await _db.ProductAttributeMappings
                .AsNoTracking()
                .Where(m => ids.Contains(m.ProductAttributeId))
                .GroupBy(m => m.ProductAttributeId)
                .Select(g => new { AttributeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AttributeId, x => x.Count, cancellationToken);

            foreach (var item in items)
                item.InUseCount = usage.TryGetValue(item.Id, out var count) ? count : 0;
        }

        return new PaginatedList<ProductAttributeDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
