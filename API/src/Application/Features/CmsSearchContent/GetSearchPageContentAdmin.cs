using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsSearchContent;

/// <summary>
/// Reads the singleton search-page content row for the admin editor (WO-105). When no row has been configured
/// yet, the in-code <see cref="SearchContentDefaults"/> are returned instead so the editor opens pre-filled.
/// </summary>
public record GetSearchPageContentAdminQuery : IRequest<CmsSearchPageContentDto>;

public class GetSearchPageContentAdminQueryHandler : IRequestHandler<GetSearchPageContentAdminQuery, CmsSearchPageContentDto>
{
    private readonly IApplicationDbContext _db;

    public GetSearchPageContentAdminQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsSearchPageContentDto> Handle(GetSearchPageContentAdminQuery request, CancellationToken cancellationToken)
    {
        // Singleton config: take the earliest row deterministically (there should only ever be one).
        var row = await _db.CMSSearchPageContents
            .AsNoTracking()
            .OrderBy(x => x.CreatedOnUtc)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? CmsSearchPageContentDto.Defaults() : CmsSearchPageContentDto.From(row);
    }
}
