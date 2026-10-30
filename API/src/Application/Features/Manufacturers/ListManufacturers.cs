using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Manufacturers;

/// <summary>Returns a page of manufacturers ordered by display order then name, optionally filtered by a name search term.</summary>
public record ListManufacturersQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<ManufacturerDto>>;

public class ListManufacturersQueryHandler : IRequestHandler<ListManufacturersQuery, PaginatedList<ManufacturerDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["slug"] = "Slug",
        ["displayOrder"] = "DisplayOrder",
        ["isEnabled"] = "IsEnabled",
    };

    private readonly IApplicationDbContext _db;

    public ListManufacturersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ManufacturerDto>> Handle(ListManufacturersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Manufacturer> query = _db.Manufacturers.AsNoTracking().Include(m => m.LogoMedia);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(m => m.Name.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name));

        var page = await PaginatedList<Manufacturer>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ManufacturerDto.From).ToList();
        return new PaginatedList<ManufacturerDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
