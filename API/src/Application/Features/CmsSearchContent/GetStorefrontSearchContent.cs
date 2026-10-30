using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.CmsBanners;
using HKPartsEcommerce.Application.Features.CmsProductCollections;

namespace HKPartsEcommerce.Application.Features.CmsSearchContent;

/// <summary>
/// Public storefront read for the search-results page (WO-105): the effective text content (stored value over
/// the in-code default, field-by-field) plus — when configured — the resolved no-results promotional banner and
/// the no-results collection's products. Always returns content, so the page renders without a seeded row.
/// </summary>
public record GetStorefrontSearchContentQuery : IRequest<StorefrontSearchContentDto>;

public class GetStorefrontSearchContentQueryHandler : IRequestHandler<GetStorefrontSearchContentQuery, StorefrontSearchContentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ISender _mediator;

    public GetStorefrontSearchContentQueryHandler(IApplicationDbContext db, IDateTimeProvider clock, ISender mediator)
    {
        _db = db;
        _clock = clock;
        _mediator = mediator;
    }

    public async Task<StorefrontSearchContentDto> Handle(GetStorefrontSearchContentQuery request, CancellationToken cancellationToken)
    {
        // Singleton config (may be absent); take the earliest row deterministically.
        var row = await _db.CMSSearchPageContents
            .AsNoTracking()
            .OrderBy(x => x.CreatedOnUtc)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Effective text: stored value where present, otherwise the in-code default (field-by-field).
        var dto = new StorefrontSearchContentDto
        {
            Heading = row?.Heading ?? SearchContentDefaults.Heading,
            PlaceholderText = row?.PlaceholderText ?? SearchContentDefaults.PlaceholderText,
            ResultsCountLabel = row?.ResultsCountLabel ?? SearchContentDefaults.ResultsCountLabel,
            NoResultsMessage = row?.NoResultsMessage ?? SearchContentDefaults.NoResultsMessage,
        };

        // Resolve the optional no-results banner, honouring the platform contract that a disabled banner or one
        // outside its active date window is never surfaced to buyers (same rule as GetActiveBanners).
        if (row?.NoResultsBannerId is Guid bannerId)
        {
            var now = _clock.UtcNow;
            var banner = await _db.CMSBanners
                .AsNoTracking()
                .Include(b => b.ImageMedia)
                .Where(b => b.Id == bannerId
                    && b.IsEnabled
                    && (b.StartsOnUtc == null || b.StartsOnUtc <= now)
                    && (b.EndsOnUtc == null || b.EndsOnUtc >= now))
                .FirstOrDefaultAsync(cancellationToken);

            if (banner is not null)
                dto.NoResultsBanner = CmsBannerPublicDto.From(banner);
        }

        // Resolve the optional no-results collection via the shared storefront projection (published only, in
        // curated order, with Customer Group pricing applied). A disabled/missing collection yields an empty list.
        if (row?.NoResultsCollectionId is Guid collectionId)
            dto.NoResultsProducts = await _mediator.Send(new GetCollectionProductsQuery(collectionId), cancellationToken);

        return dto;
    }
}
