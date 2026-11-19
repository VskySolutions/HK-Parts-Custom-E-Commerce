using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Ffl;

/// <summary>
/// Radius search of the dealer directory (FFL-30…39). ZIP must be five digits (400 otherwise) and the radius
/// one of the configured values for the calling surface (FFL-36: an arbitrary radius such as 5000 is refused).
/// Every search is logged (FFL-35/112).
/// </summary>
public record SearchFflDealersQuery(string? Zip, int? Radius, FflSearchContext Context) : IRequest<FflSearchResultDto>;

public class SearchFflDealersQueryHandler : IRequestHandler<SearchFflDealersQuery, FflSearchResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IZipGeocoder _geocoder;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;
    private readonly ILogger<SearchFflDealersQueryHandler> _logger;

    public SearchFflDealersQueryHandler(
        IApplicationDbContext db, IZipGeocoder geocoder, ISettingsService settings, IDateTimeProvider clock,
        ICurrentUserService current, ILogger<SearchFflDealersQueryHandler> logger)
    {
        _db = db;
        _geocoder = geocoder;
        _settings = settings;
        _clock = clock;
        _current = current;
        _logger = logger;
    }

    public async Task<FflSearchResultDto> Handle(SearchFflDealersQuery request, CancellationToken ct)
    {
        var settings = await FflSettingsStore.LoadAsync(_settings, ct);
        if (request.Context != FflSearchContext.Admin && !settings.Enabled)
            throw new NotFoundException("The FFL dealer search is not available.");

        var zip = FflDirectory.NormalizeZip(request.Zip)
            ?? throw new ValidationException(new[] { new ValidationFailure("zip", FflMessages.InvalidZip) });

        var allowed = request.Context == FflSearchContext.Public ? settings.PublicRadii : settings.CheckoutRadii;
        var radius = request.Radius ?? allowed[0];
        if (!allowed.Contains(radius))
            throw new ValidationException(new[]
            {
                new ValidationFailure("radius", $"Choose a search radius of {string.Join(", ", allowed)} miles."),
            });

        var result = await FflDirectory.SearchAsync(_db, _geocoder, zip, radius, settings.MaxResults, _clock.UtcNow, _logger, ct);

        _logger.LogInformation(
            "FFL radius-search {Context}: ZIP {Zip}, radius {Radius} mi, {Found} dealer(s) found, IP {Ip}, user {UserId}.",
            request.Context, zip, radius, result.TotalFound, _current.IpAddress, _current.UserId);
        return result;
    }
}

/// <summary>Dealer details by directory key (§6.1 <c>GET …/dealers/{key}</c>): 400 for a malformed key, 404 when unknown.</summary>
public record GetFflDealerQuery(string Key) : IRequest<FflDealerDto>;

public class GetFflDealerQueryHandler : IRequestHandler<GetFflDealerQuery, FflDealerDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetFflDealerQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<FflDealerDto> Handle(GetFflDealerQuery request, CancellationToken ct)
    {
        if (!FflDirectory.IsValidKey(request.Key))
            throw new ValidationException(new[] { new ValidationFailure("key", "A dealer key is digits only.") });
        var dealer = await FflDirectory.FindAsync(_db, request.Key, ct)
            ?? throw new NotFoundException(FflMessages.DealerLoadFailed);
        return FflDirectory.ToDto(dealer, _clock.UtcNow);
    }
}
