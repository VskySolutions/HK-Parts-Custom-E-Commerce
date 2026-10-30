using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsBanners;

/// <summary>
/// Returns the enabled banners for a storefront placement (<see cref="Location"/>) whose active date
/// range currently contains "now". A null start or end bound is treated as open on that side, so a banner
/// with neither bound set is always active. Results are ordered by display order. This is the public
/// contract guaranteeing that banners outside their active date range are never surfaced to buyers.
/// </summary>
public record GetActiveBannersQuery(string Location) : IRequest<IReadOnlyList<CmsBannerPublicDto>>;

public class GetActiveBannersQueryHandler : IRequestHandler<GetActiveBannersQuery, IReadOnlyList<CmsBannerPublicDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetActiveBannersQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CmsBannerPublicDto>> Handle(GetActiveBannersQuery request, CancellationToken cancellationToken)
    {
        var location = request.Location?.Trim();
        if (string.IsNullOrEmpty(location))
            return Array.Empty<CmsBannerPublicDto>();

        var now = _clock.UtcNow;

        var banners = await _db.CMSBanners
            .AsNoTracking()
            .Include(b => b.ImageMedia)
            .Where(b => b.IsEnabled
                && b.DisplayLocation == location
                && (b.StartsOnUtc == null || b.StartsOnUtc <= now)
                && (b.EndsOnUtc == null || b.EndsOnUtc >= now))
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

        return banners.Select(CmsBannerPublicDto.From).ToList();
    }
}
