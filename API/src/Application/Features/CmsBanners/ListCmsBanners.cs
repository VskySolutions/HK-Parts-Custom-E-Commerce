using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsBanners;

/// <summary>
/// Returns a page of banners ordered by display location then display order, optionally filtered by
/// display location, enabled state, and a title search term.
/// </summary>
public record ListCmsBannersQuery(
    int Page = 1,
    int PageSize = 20,
    string? DisplayLocation = null,
    bool? IsEnabled = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<CmsBannerDto>>;

public class ListCmsBannersQueryHandler : IRequestHandler<ListCmsBannersQuery, PaginatedList<CmsBannerDto>>
{
    // Grid column name -> entity property path. Anything else falls back to the natural order below.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "Title",
        ["displayLocation"] = "DisplayLocation",
        ["displayOrder"] = "DisplayOrder",
        ["startsOnUtc"] = "StartsOnUtc",
        ["endsOnUtc"] = "EndsOnUtc",
        ["isEnabled"] = "IsEnabled",
    };

    private readonly IApplicationDbContext _db;

    public ListCmsBannersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CmsBannerDto>> Handle(ListCmsBannersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<CMSBanner> query = _db.CMSBanners.AsNoTracking().Include(b => b.ImageMedia);

        if (!string.IsNullOrWhiteSpace(request.DisplayLocation))
        {
            var location = request.DisplayLocation.Trim();
            query = query.Where(b => b.DisplayLocation == location);
        }

        if (request.IsEnabled.HasValue)
            query = query.Where(b => b.IsEnabled == request.IsEnabled.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(b => b.Title.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(b => b.DisplayLocation).ThenBy(b => b.DisplayOrder));

        var page = await PaginatedList<CMSBanner>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(CmsBannerDto.From).ToList();
        return new PaginatedList<CmsBannerDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
