using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsPageGroups;

/// <summary>Returns all CMS page groups ordered by display order then name (non-paged).</summary>
public record ListCmsPageGroupsQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool SortDescending = false) : IRequest<PaginatedList<CmsPageGroupDto>>;

public class ListCmsPageGroupsQueryHandler : IRequestHandler<ListCmsPageGroupsQuery, PaginatedList<CmsPageGroupDto>>
{

    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["slug"] = "Slug",
        ["displayOrder"] = "DisplayOrder",
    };

    private readonly IApplicationDbContext _db;

    public ListCmsPageGroupsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CmsPageGroupDto>> Handle(ListCmsPageGroupsQuery request, CancellationToken cancellationToken)
    {

        IQueryable<CMSPageGroup> query = _db.CMSPageGroups.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(f => f.Name.Contains(term) || f.Slug == null || f.Slug.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name));

        var page = await PaginatedList<CMSPageGroup>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(CmsPageGroupDto.From).ToList();
        return new PaginatedList<CmsPageGroupDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
