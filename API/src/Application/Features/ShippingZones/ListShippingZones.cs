using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ShippingZones;

/// <summary>Returns a page of shipping zones ordered by name, optionally filtered by a name/country search term and/or enabled state.</summary>
public record ListShippingZonesQuery(int Page = 1, int PageSize = 20, string? Search = null, string? CountryCode = null, bool? IsEnabled = null, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<ShippingZoneDto>>;

public class ListShippingZonesQueryHandler : IRequestHandler<ListShippingZonesQuery, PaginatedList<ShippingZoneDto>>
{
    // Column name (from the grid) -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["countryCode"] = "CountryCode",
        ["region"] = "Region",
        ["isEnabled"] = "IsEnabled",
    };

    private readonly IApplicationDbContext _db;

    public ListShippingZonesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ShippingZoneDto>> Handle(ListShippingZonesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ShippingZone> query = _db.ShippingZones.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(z => z.Name.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var country = request.CountryCode.Trim();
            query = query.Where(z => z.CountryCode == country);
        }

        if (request.IsEnabled.HasValue)
            query = query.Where(z => z.IsEnabled == request.IsEnabled.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(z => z.Name));

        var page = await PaginatedList<ShippingZone>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ShippingZoneDto.From).ToList();
        return new PaginatedList<ShippingZoneDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
