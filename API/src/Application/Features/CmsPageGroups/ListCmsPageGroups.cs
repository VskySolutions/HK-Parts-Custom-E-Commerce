using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsPageGroups;

/// <summary>Returns all CMS page groups ordered by display order then name (non-paged).</summary>
public record ListCmsPageGroupsQuery : IRequest<IReadOnlyList<CmsPageGroupDto>>;

public class ListCmsPageGroupsQueryHandler
    : IRequestHandler<ListCmsPageGroupsQuery, IReadOnlyList<CmsPageGroupDto>>
{
    private readonly IApplicationDbContext _db;

    public ListCmsPageGroupsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CmsPageGroupDto>> Handle(ListCmsPageGroupsQuery request, CancellationToken cancellationToken)
    {
        var groups = await _db.CMSPageGroups
            .AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return groups.Select(CmsPageGroupDto.From).ToList();
    }
}
