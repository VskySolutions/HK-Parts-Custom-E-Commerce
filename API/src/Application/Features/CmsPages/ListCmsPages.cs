using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsPages;

/// <summary>Returns a page of CMS pages ordered by display order then title, filterable by status,
/// owning group and a free-text term matched against the title or slug.</summary>
public record ListCmsPagesQuery(
    CmsContentStatus? Status = null,
    Guid? PageGroupId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<CmsPageDto>>;

public class ListCmsPagesQueryHandler : IRequestHandler<ListCmsPagesQuery, PaginatedList<CmsPageDto>>
{
    // Grid column name -> entity property path. Anything else falls back to the natural order below.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "Title",
        ["slug"] = "Slug",
        ["status"] = "Status",
        ["displayOrder"] = "DisplayOrder",
        ["createdOnUtc"] = "CreatedOnUtc",
    };

    private readonly IApplicationDbContext _db;

    public ListCmsPagesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CmsPageDto>> Handle(ListCmsPagesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<CMSPage> query = _db.CMSPages.AsNoTracking().Include(p => p.PageGroup);

        if (request.Status is CmsContentStatus status)
            query = query.Where(p => p.Status == status);

        if (request.PageGroupId is Guid groupId)
            query = query.Where(p => p.PageGroupId == groupId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.Title.Contains(term) || p.Slug.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Title));

        var page = await PaginatedList<CMSPage>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(CmsPageDto.From).ToList();
        return new PaginatedList<CmsPageDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
