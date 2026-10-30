using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ShippingMethods;

/// <summary>
/// Returns a page of shipping methods ordered by display order then name, optionally filtered by a name
/// search term, calculation type and/or enabled state. Each method carries its per-zone rate overrides.
/// </summary>
public record ListShippingMethodsQuery(int Page = 1, int PageSize = 20, string? Search = null, ShippingMethodType? MethodType = null, bool? IsEnabled = null, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<ShippingMethodDto>>;

public class ListShippingMethodsQueryHandler : IRequestHandler<ListShippingMethodsQuery, PaginatedList<ShippingMethodDto>>
{
    // Column name (from the grid) -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["methodType"] = "MethodType",
        ["flatRate"] = "FlatRate",
        ["displayOrder"] = "DisplayOrder",
        ["isEnabled"] = "IsEnabled",
    };

    private readonly IApplicationDbContext _db;

    public ListShippingMethodsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ShippingMethodDto>> Handle(ListShippingMethodsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ShippingMethod> query = _db.ShippingMethods
            .AsNoTracking()
            .Include(m => m.ZoneRates)
                .ThenInclude(r => r.ShippingZone);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(m => m.Name.Contains(term));
        }

        if (request.MethodType.HasValue)
            query = query.Where(m => m.MethodType == request.MethodType.Value);

        if (request.IsEnabled.HasValue)
            query = query.Where(m => m.IsEnabled == request.IsEnabled.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name));

        var page = await PaginatedList<ShippingMethod>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ShippingMethodDto.From).ToList();
        return new PaginatedList<ShippingMethodDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
