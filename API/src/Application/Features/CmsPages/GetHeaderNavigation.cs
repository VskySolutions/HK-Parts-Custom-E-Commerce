using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsPages;

/// <summary>Public storefront top-bar navigation: Published pages flagged
/// <see cref="CMSPage.ShowInTopBar"/>, ordered by display order then title. Rendered as quick-access
/// links in the storefront navigation bar.</summary>
public record GetHeaderNavigationQuery : IRequest<IReadOnlyList<CmsNavPageDto>>;

public class GetHeaderNavigationQueryHandler : IRequestHandler<GetHeaderNavigationQuery, IReadOnlyList<CmsNavPageDto>>
{
    private readonly IApplicationDbContext _db;

    public GetHeaderNavigationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CmsNavPageDto>> Handle(GetHeaderNavigationQuery request, CancellationToken cancellationToken)
    {
        var pages = await _db.CMSPages
            .AsNoTracking()
            .Where(p => p.Status == CmsContentStatus.Published && p.ShowInTopBar)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Title)
            .Select(p => new CmsNavPageDto { Title = p.Title, Slug = p.Slug })
            .ToListAsync(cancellationToken);

        return pages;
    }
}
