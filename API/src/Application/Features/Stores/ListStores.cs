using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Stores;

/// <summary>Returns a page of stores ordered by name, optionally filtered by a name search term, enabled state, guest-ordering and/or pickup availability.</summary>
public record ListStoresQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsEnabled = null,
    bool? GuestOrderingEnabled = null,
    bool? PickupEnabled = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<StoreDto>>;

public class ListStoresQueryHandler : IRequestHandler<ListStoresQuery, PaginatedList<StoreDto>>
{
    // Column name (from the grid) -> entity property path. Anything else falls back to CreatedOnUtc desc.
    // (Location/status are computed display columns — City is a [NotMapped] read-through, so they are not sortable.)
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["guestOrderingEnabled"] = "GuestOrderingEnabled",
    };

    private readonly IApplicationDbContext _db;

    public ListStoresQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<StoreDto>> Handle(ListStoresQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Store> query = _db.Stores.AsNoTracking().Include(s => s.Address);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(s => s.Name.Contains(term));
        }

        if (request.IsEnabled.HasValue)
            query = query.Where(s => s.IsEnabled == request.IsEnabled.Value);

        if (request.GuestOrderingEnabled.HasValue)
            query = query.Where(s => s.GuestOrderingEnabled == request.GuestOrderingEnabled.Value);

        if (request.PickupEnabled.HasValue)
            query = query.Where(s => s.PickupEnabled == request.PickupEnabled.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(s => s.Name));

        var page = await PaginatedList<Store>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(StoreDto.From).ToList();
        return new PaginatedList<StoreDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
