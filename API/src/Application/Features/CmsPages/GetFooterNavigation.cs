using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsPages;

/// <summary>A single footer/nav column: a page group and its published pages.</summary>
public class CmsNavGroupDto
{
    public string GroupName { get; set; } = string.Empty;
    public string? GroupSlug { get; set; }
    public List<CmsNavPageDto> Pages { get; set; } = new();
}

/// <summary>A published page link within a footer/nav column.</summary>
public class CmsNavPageDto
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

/// <summary>Public storefront navigation: Published pages flagged <see cref="CMSPage.ShowInFooter"/>,
/// grouped by their CMS page group, ordered by group display order then page display order (drives
/// footer/nav link columns). Groups with no qualifying pages are omitted.</summary>
public record GetFooterNavigationQuery : IRequest<IReadOnlyList<CmsNavGroupDto>>;

public class GetFooterNavigationQueryHandler : IRequestHandler<GetFooterNavigationQuery, IReadOnlyList<CmsNavGroupDto>>
{
    private readonly IApplicationDbContext _db;

    public GetFooterNavigationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CmsNavGroupDto>> Handle(GetFooterNavigationQuery request, CancellationToken cancellationToken)
    {
        var groups = await _db.CMSPageGroups
            .AsNoTracking()
            .Where(g => g.Pages.Any(p => p.Status == CmsContentStatus.Published && p.ShowInFooter))
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Name)
            .Select(g => new CmsNavGroupDto
            {
                GroupName = g.Name,
                GroupSlug = g.Slug,
                Pages = g.Pages
                    .Where(p => p.Status == CmsContentStatus.Published && p.ShowInFooter)
                    .OrderBy(p => p.DisplayOrder)
                    .ThenBy(p => p.Title)
                    .Select(p => new CmsNavPageDto { Title = p.Title, Slug = p.Slug })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return groups;
    }
}
