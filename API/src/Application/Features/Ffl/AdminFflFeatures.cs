using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Ffl;

/// <summary>Directory health for the admin page.</summary>
public class FflDirectoryStatsDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Geocoded { get; set; }
    /// <summary>Active dealers without a geocode: they never appear in a radius search.</summary>
    public int MissingGeocode { get; set; }
    public int TransfersRefused { get; set; }
    public DateTime? LastImportedOnUtc { get; set; }
    public bool GeocoderConfigured { get; set; }
}

/// <summary>Admin directory grid: search by key, name, city, ZIP or licence number; optional state filter.</summary>
public record ListFflDealersQuery(string? Search, string? State, bool? ActiveOnly, int Page = 1, int PageSize = 25)
    : IRequest<PaginatedList<FflDealerDto>>;

public class ListFflDealersQueryHandler : IRequestHandler<ListFflDealersQuery, PaginatedList<FflDealerDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ListFflDealersQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PaginatedList<FflDealerDto>> Handle(ListFflDealersQuery request, CancellationToken ct)
    {
        var query = _db.FflDealers.AsNoTracking();
        if (request.ActiveOnly == true)
            query = query.Where(d => d.IsActive);
        if (!string.IsNullOrWhiteSpace(request.State))
        {
            var state = request.State.Trim();
            query = query.Where(d => d.PremiseState == state);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            // A pasted licence number searches by its key.
            var key = FflDirectory.KeyFromFflNumber(term) ?? term;
            query = query.Where(d => d.Key == key
                                     || (d.BusinessName != null && d.BusinessName.Contains(term))
                                     || (d.LicenseName != null && d.LicenseName.Contains(term))
                                     || (d.PremiseCity != null && d.PremiseCity.Contains(term))
                                     || (d.PremiseZipCode != null && d.PremiseZipCode.StartsWith(term)));
        }

        var ordered = query.OrderBy(d => d.PremiseState).ThenBy(d => d.PremiseCity).ThenBy(d => d.BusinessName ?? d.LicenseName);
        var page = await PaginatedList<FflDealer>.CreateAsync(ordered, request.Page, request.PageSize, ct);
        var now = _clock.UtcNow;
        return new PaginatedList<FflDealerDto>(
            page.Items.Select(d => FflDirectory.ToDto(d, now)).ToList(), page.TotalCount, page.PageNumber, page.PageSize);
    }
}

public record GetFflDirectoryStatsQuery : IRequest<FflDirectoryStatsDto>;

public class GetFflDirectoryStatsQueryHandler : IRequestHandler<GetFflDirectoryStatsQuery, FflDirectoryStatsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IZipGeocoder _geocoder;

    public GetFflDirectoryStatsQueryHandler(IApplicationDbContext db, IZipGeocoder geocoder)
    {
        _db = db;
        _geocoder = geocoder;
    }

    public async Task<FflDirectoryStatsDto> Handle(GetFflDirectoryStatsQuery request, CancellationToken ct)
    {
        var dealers = _db.FflDealers.AsNoTracking();
        return new FflDirectoryStatsDto
        {
            Total = await dealers.CountAsync(ct),
            Active = await dealers.CountAsync(d => d.IsActive, ct),
            Geocoded = await dealers.CountAsync(d => d.Latitude != null && d.Longitude != null, ct),
            MissingGeocode = await dealers.CountAsync(d => d.IsActive && (d.Latitude == null || d.Longitude == null), ct),
            TransfersRefused = await dealers.CountAsync(d => d.AllowsTransfers == false, ct),
            LastImportedOnUtc = await dealers.MaxAsync(d => d.LastImportedOnUtc, ct),
            GeocoderConfigured = await _geocoder.IsConfiguredAsync(ct),
        };
    }
}

/// <summary>
/// Update Dealer Data (FFL-94/95, §6.1 <c>PUT /api/admin/ffl/dealers/{key}/data</c>): whether the dealer accepts
/// transfers and their last transfer fee. The fee is only changed when one is entered. A dealer set to refuse
/// transfers drops out of every radius search (FFL-96).
/// </summary>
public record UpdateFflDealerDataCommand(string Key, bool AllowsTransfers, decimal? LastTransferFee, decimal? AverageReviewRating = null)
    : IRequest<FflDealerDto>;

public class UpdateFflDealerDataCommandValidator : AbstractValidator<UpdateFflDealerDataCommand>
{
    public UpdateFflDealerDataCommandValidator()
    {
        RuleFor(x => x.Key).Must(FflDirectory.IsValidKey).WithMessage("A dealer key is digits only.");
        RuleFor(x => x.LastTransferFee).GreaterThanOrEqualTo(0).LessThan(100000).When(x => x.LastTransferFee.HasValue)
            .WithMessage("Transfer fee must be a positive amount.");
        RuleFor(x => x.AverageReviewRating).InclusiveBetween(0, 5).When(x => x.AverageReviewRating.HasValue);
    }
}

public class UpdateFflDealerDataCommandHandler : IRequestHandler<UpdateFflDealerDataCommand, FflDealerDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateFflDealerDataCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<FflDealerDto> Handle(UpdateFflDealerDataCommand request, CancellationToken ct)
    {
        var dealer = await FflDirectory.FindAsync(_db, request.Key, ct, track: true)
            ?? throw new NotFoundException(FflMessages.DealerLoadFailed);
        dealer.AllowsTransfers = request.AllowsTransfers;
        if (request.LastTransferFee.HasValue)
            dealer.LastTransferFee = decimal.Round(request.LastTransferFee.Value, 2);
        if (request.AverageReviewRating.HasValue)
            dealer.AverageReviewRating = request.AverageReviewRating;
        dealer.DealerDataUpdatedOnUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return FflDirectory.ToDto(dealer, _clock.UtcNow);
    }
}

/// <summary>Result of an admin dealer change.</summary>
public class ChangeOrderFflDealerResult
{
    public string Message { get; set; } = FflMessages.AdminDealerChanged;
    public FflDealerDto Dealer { get; set; } = new();
}

/// <summary>
/// Select New Shipping Dealer on an order (FFL-92/93, §6.1 <c>PUT /api/admin/orders/{id}/ffl-dealer</c>). The
/// dealer is read from the directory, the order's FFL shipment is re-pointed at it, and an order history note
/// records the old and new dealer and the admin. Refused once the order has shipped (the legacy screen only hid
/// the link), and for a dealer that is unknown, expired or refusing transfers.
/// </summary>
public record ChangeOrderFflDealerCommand(Guid OrderId, string DealerKey) : IRequest<ChangeOrderFflDealerResult>;

public class ChangeOrderFflDealerCommandValidator : AbstractValidator<ChangeOrderFflDealerCommand>
{
    public ChangeOrderFflDealerCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.DealerKey).NotEmpty().WithMessage(FflMessages.DealerRequired)
            .Must(FflDirectory.IsValidKey).WithMessage("A dealer key is digits only.");
    }
}

public class ChangeOrderFflDealerCommandHandler : IRequestHandler<ChangeOrderFflDealerCommand, ChangeOrderFflDealerResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ChangeOrderFflDealerCommandHandler> _logger;

    public ChangeOrderFflDealerCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, IDateTimeProvider clock, ILogger<ChangeOrderFflDealerCommandHandler> logger)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ChangeOrderFflDealerResult> Handle(ChangeOrderFflDealerCommand request, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Shippings)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.Status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.Cancelled)
            throw new ConflictException($"The FFL dealer cannot be changed on an order that is {order.Status}.");

        var shipment = order.Shippings.FirstOrDefault(s => s.ShipmentType == OrderShipmentType.Ffl)
                       ?? order.Shippings.FirstOrDefault(s => s.ContainsFflItems)
                       ?? throw new ConflictException("This order has no FFL shipment.");

        var now = _clock.UtcNow;
        var dealer = await FflDirectory.FindAsync(_db, request.DealerKey, ct)
            ?? throw new NotFoundException(FflMessages.DealerLoadFailed);
        if (FflDirectory.IneligibleReason(dealer, now) is string reason)
            throw CheckoutErrorCodes.Conflict(FflMessages.DealerNotEligibleCode, reason);

        var before = shipment.FflDealerName is null
            ? "none"
            : $"{shipment.FflDealerName} (FFL {shipment.FflNumber})";
        FflDirectory.ApplyToShipping(shipment, dealer, FflDealerSource.Admin);
        shipment.ContainsFflItems = true;
        order.HasFflItems = true;

        var by = _current.Email ?? _current.UserId?.ToString() ?? "an administrator";
        _db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = order.Status,
            ChangedById = _current.UserId,
            ChangedOnUtc = now,
            Note = $"FFL dealer changed by {by}: from {before} to {shipment.FflDealerName} (FFL {shipment.FflNumber}).",
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FFL dealer on order {OrderNumber} changed to {Key} by {User}.", order.OrderNumber, dealer.Key, by);
        return new ChangeOrderFflDealerResult { Dealer = FflDirectory.ToDto(dealer, now) };
    }
}

/// <summary>Result of a geocoding run.</summary>
public class GeocodeFflDealersResult
{
    public int ZipsResolved { get; set; }
    public int ZipsNotFound { get; set; }
    public int DealersUpdated { get; set; }
    public int RemainingWithoutGeocode { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Gives dealers without a position the centre of their premise ZIP (FFL-113), resolving at most
/// <see cref="MaxZips"/> ZIPs per run through the cache and the geocoding provider. Re-run until nothing remains.
/// </summary>
public record GeocodeFflDealersCommand(int MaxZips = 500) : IRequest<GeocodeFflDealersResult>;

public class GeocodeFflDealersCommandHandler : IRequestHandler<GeocodeFflDealersCommand, GeocodeFflDealersResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IZipGeocoder _geocoder;
    private readonly ILogger<GeocodeFflDealersCommandHandler> _logger;

    public GeocodeFflDealersCommandHandler(IApplicationDbContext db, IZipGeocoder geocoder, ILogger<GeocodeFflDealersCommandHandler> logger)
    {
        _db = db;
        _geocoder = geocoder;
        _logger = logger;
    }

    public async Task<GeocodeFflDealersResult> Handle(GeocodeFflDealersCommand request, CancellationToken ct)
    {
        var result = new GeocodeFflDealersResult();
        var limit = Math.Clamp(request.MaxZips, 1, 2000);

        var zips = (await _db.FflDealers.AsNoTracking()
                .Where(d => d.IsActive && (d.Latitude == null || d.Longitude == null) && d.PremiseZipCode != null)
                .Select(d => d.PremiseZipCode!)
                .Distinct()
                .ToListAsync(ct))
            .Select(FflDirectory.NormalizeZip)
            .Where(z => z is not null)
            .Select(z => z!)
            .Distinct()
            .Take(limit)
            .ToList();

        var configured = await _geocoder.IsConfiguredAsync(ct);
        foreach (var zip in zips)
        {
            GeoPoint? point = null;
            var cached = await _db.ZipGeocodes.AsNoTracking().FirstOrDefaultAsync(z => z.Zip == zip, ct);
            if (cached is not null)
                point = new GeoPoint(cached.Latitude, cached.Longitude);
            else if (configured)
            {
                try
                {
                    point = await _geocoder.GeocodeZipAsync(zip, ct);
                    if (point is { } p)
                        _db.ZipGeocodes.Add(new ZipGeocode { Zip = zip, Latitude = p.Latitude, Longitude = p.Longitude, Source = "Geocodio" });
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
                {
                    _logger.LogWarning(ex, "FFL geocoding: ZIP {Zip} failed; stopping this run.", zip);
                    result.Message = FflMessages.SearchFailed;
                    break;
                }
            }

            if (point is not { } at)
            {
                result.ZipsNotFound++;
                continue;
            }

            var dealers = await _db.FflDealers
                .Where(d => (d.Latitude == null || d.Longitude == null) && d.PremiseZipCode != null && d.PremiseZipCode.StartsWith(zip))
                .ToListAsync(ct);
            foreach (var d in dealers)
            {
                d.Latitude = at.Latitude;
                d.Longitude = at.Longitude;
            }
            result.ZipsResolved++;
            result.DealersUpdated += dealers.Count;
            await _db.SaveChangesAsync(ct);
        }

        result.RemainingWithoutGeocode = await _db.FflDealers.CountAsync(d => d.IsActive && (d.Latitude == null || d.Longitude == null), ct);
        if (!configured && result.ZipsResolved == 0 && result.RemainingWithoutGeocode > 0)
            result.Message ??= "Add a Geocodio API key in the FFL settings to geocode dealers, or import a file that includes latitude and longitude.";
        return result;
    }
}
