using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsHomeSections;

/// <summary>Lists every home page section (enabled and disabled) in display order, for the admin management
/// screen. Non-paged — the section list is small and fully rendered as an orderable layout.</summary>
public record ListHomeSectionsQuery() : IRequest<IReadOnlyList<CmsHomeSectionDto>>;

public class ListHomeSectionsQueryHandler : IRequestHandler<ListHomeSectionsQuery, IReadOnlyList<CmsHomeSectionDto>>
{
    private readonly IApplicationDbContext _db;

    public ListHomeSectionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CmsHomeSectionDto>> Handle(ListHomeSectionsQuery request, CancellationToken cancellationToken)
    {
        var sections = await _db.CMSHomePageSections
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return sections.Select(CmsHomeSectionDto.From).ToList();
    }
}
