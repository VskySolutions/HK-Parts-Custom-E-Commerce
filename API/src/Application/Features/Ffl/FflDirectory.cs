using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Ffl;

/// <summary>
/// The FFL dealer directory rules (FFL shipping requirements §5.4–5.6, §6): ZIP geocoding, the radius search,
/// licence-number building and parsing, and dealer eligibility. The directory is queried directly from the
/// <see cref="FflDealer"/> table (the rebuild's answer to FFL-110/111: the browser only ever calls our API, and
/// no directory credentials exist to leak). Static so the checkout orchestrator and the MediatR handlers share
/// one implementation without extra DI wiring.
/// </summary>
public static partial class FflDirectory
{
    /// <summary>Earth radius in miles, as the legacy haversine query (FFL-33).</summary>
    public const double EarthRadiusMiles = 3959.0;

    /// <summary>Licence types that may receive a transfer (FFL-33): dealer, pawnbroker, manufacturer, importer and
    /// the destructive-device types. 03 (collector) and 06 (ammunition manufacturer) are excluded.</summary>
    public static readonly string[] TransferLicenseTypes = { "01", "02", "07", "08", "09", "10", "11" };

    [GeneratedRegex("^[0-9]{5}$")]
    private static partial Regex Zip5Regex();

    [GeneratedRegex("^[0-9]{3,20}$")]
    private static partial Regex KeyRegex();

    /// <summary>FFL-31: the first five characters, when they are five digits; otherwise null.</summary>
    public static string? NormalizeZip(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
            return null;
        var trimmed = zip.Trim();
        if (trimmed.Length < 5)
            return null;
        var five = trimmed[..5];
        return Zip5Regex().IsMatch(five) ? five : null;
    }

    /// <summary>FFL-58: a dealer key is digits only.</summary>
    public static bool IsValidKey(string? key) => key is not null && KeyRegex().IsMatch(key.Trim());

    public static string DealerKey(string regn, string dist, string seqn) => $"{regn.Trim()}{dist.Trim()}{seqn.Trim()}";

    /// <summary>FFL-53: <c>lic_regn-lic_dist-lic_cnty(3)-lic_type(2)-lic_xprdte-lic_seqn</c>.</summary>
    public static string BuildFflNumber(FflDealer d) =>
        $"{d.LicRegn}-{d.LicDist}-{d.LicCnty.PadLeft(3, '0')}-{d.LicType.PadLeft(2, '0')}-{d.LicXprdte}-{d.LicSeqn}";

    /// <summary>
    /// The directory key inside a stored FFL number (legacy <c>Helper::getDealerKey</c>), or null when the
    /// number is not in the R-DD-CCC-TT-XX-SSSSS shape (FFL-26: a hand-typed number must not throw).
    /// </summary>
    public static string? KeyFromFflNumber(string? fflNumber)
    {
        if (string.IsNullOrWhiteSpace(fflNumber))
            return null;
        var parts = fflNumber.Trim().Split('-');
        if (parts.Length != 6)
            return null;
        var key = DealerKey(parts[0], parts[1], parts[5]);
        return IsValidKey(key) ? key : null;
    }

    /// <summary>Business name (trimmed), else licence name (FFL-40/53).</summary>
    public static string DisplayName(FflDealer d) =>
        !string.IsNullOrWhiteSpace(d.BusinessName) ? d.BusinessName.Trim() : (d.LicenseName ?? string.Empty).Trim();

    /// <summary>FFL-42/46: "(xxx) xxx-xxxx" for exactly ten digits, otherwise the raw value.</summary>
    public static string? FormatPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length == 10 ? $"({digits[..3]}) {digits[3..6]}-{digits[6..]}" : phone.Trim();
    }

    /// <summary>FFL-40: Google Maps search of the premise address.</summary>
    public static string MapUrl(string? street, string? city, string? state, string? zip) =>
        "https://www.google.com/maps/search/?api=1&query=" +
        Uri.EscapeDataString($"{street} {city}, {state} {zip}".Trim());

    /// <summary>ATF expiry month letters, January first. There is no I.</summary>
    private const string MonthLetters = "ABCDEFGHJKLM";

    /// <summary>
    /// The last day of the licence's expiry month. The ATF code is the last digit of the year followed by a
    /// month letter A–M with no I (A = January … H = August, J = September … M = December — the ATF
    /// skips I so it is not read as 1); the decade is the one that puts the year closest to today
    /// (licences run three years). Null when the code cannot be read.
    /// </summary>
    public static DateOnly? LicenseExpiry(string? code, DateTime todayUtc)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length < 2)
            return null;
        var c = code.Trim().ToUpperInvariant();
        var month = MonthLetters.IndexOf(c[1]) + 1;
        if (!char.IsDigit(c[0]) || month == 0)
            return null;

        var digit = c[0] - '0';
        var year = todayUtc.Year - todayUtc.Year % 10 + digit;
        if (year > todayUtc.Year + 4) year -= 10;
        else if (year < todayUtc.Year - 5) year += 10;
        return new DateOnly(year, month, DateTime.DaysInMonth(year, month));
    }

    public static bool IsExpired(FflDealer d, DateTime todayUtc) =>
        LicenseExpiry(d.LicXprdte, todayUtc) is { } expires && expires < DateOnly.FromDateTime(todayUtc);

    /// <summary>
    /// Why the dealer cannot receive a checkout shipment (FFL-26/59), or null when it can: it must still be in
    /// the directory, hold a transfer licence type, have an unexpired licence and not refuse transfers.
    /// </summary>
    public static string? IneligibleReason(FflDealer d, DateTime todayUtc)
    {
        if (!d.IsActive)
            return "This dealer is no longer listed in the licensed dealer directory. Please choose another dealer.";
        if (d.AllowsTransfers == false || !TransferLicenseTypes.Contains(d.LicType))
            return FflMessages.DealerNotEligible;
        if (IsExpired(d, todayUtc))
            return "This dealer's license has expired. Please choose another dealer.";
        return null;
    }

    public static FflDealerDto ToDto(FflDealer d, DateTime todayUtc, double? distanceMiles = null)
    {
        var expiry = LicenseExpiry(d.LicXprdte, todayUtc);
        var reason = IneligibleReason(d, todayUtc);
        return new FflDealerDto
        {
            Key = d.Key,
            Name = DisplayName(d),
            LicenseName = d.LicenseName,
            BusinessName = d.BusinessName,
            Street = d.PremiseStreet,
            City = d.PremiseCity,
            State = d.PremiseState,
            Zip = d.PremiseZipCode,
            Phone = d.VoicePhone,
            PhoneDisplay = FormatPhone(d.VoicePhone),
            DistanceMiles = distanceMiles is { } m ? Math.Round(m, 1) : null,
            LicenseType = d.LicType,
            LicenseNumber = BuildFflNumber(d),
            LicenseExpires = expiry?.ToString("yyyy-MM"),
            LicenseExpired = expiry is { } e && e < DateOnly.FromDateTime(todayUtc),
            AllowsTransfers = d.AllowsTransfers != false,
            AllowsTransfersRecorded = d.AllowsTransfers.HasValue,
            LastTransferFee = d.LastTransferFee,
            AverageReviewRating = d.AverageReviewRating,
            IsActive = d.IsActive,
            IsEligible = reason is null,
            IneligibleReason = reason,
            MapUrl = MapUrl(d.PremiseStreet, d.PremiseCity, d.PremiseState, d.PremiseZipCode),
            HasGeocode = d.Latitude.HasValue && d.Longitude.HasValue,
            IsManual = d.IsManual,
        };
    }

    public static Task<FflDealer?> FindAsync(IApplicationDbContext db, string? key, CancellationToken ct, bool track = false)
    {
        if (!IsValidKey(key))
            return Task.FromResult<FflDealer?>(null);
        var k = key!.Trim();
        var query = track ? db.FflDealers : db.FflDealers.AsNoTracking();
        return query.FirstOrDefaultAsync(d => d.Key == k, ct);
    }

    /// <summary>Copies the dealer onto a shipment row as its ship-to (FFL-53/80; admin change FFL-92).</summary>
    public static void ApplyToShipping(OrderShipping s, FflDealer d, FflDealerSource source)
    {
        s.ShipToType = ShipToType.FflDealer;
        s.FflDealerKey = d.Key;
        s.FflDealerName = DisplayName(d);
        s.FflNumber = BuildFflNumber(d);
        s.FflDealerStreet = d.PremiseStreet?.Trim();
        s.FflDealerCity = d.PremiseCity?.Trim();
        s.FflDealerState = d.PremiseState?.Trim();
        s.FflDealerZip = d.PremiseZipCode?.Trim();
        s.FflDealerPhone = d.VoicePhone?.Trim();
        s.FflSource = source;
    }

    /// <summary>Great-circle distance in miles (FFL-33).</summary>
    public static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double deg) => deg * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusMiles * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>
    /// The centre of a ZIP (FFL-32): the cached geocode, else the geocoding provider (cached on success), else
    /// the average position of the directory's own dealers in that ZIP. <c>Failed</c> is true when the provider
    /// errored or timed out, so the caller can tell "unknown ZIP" from "service down".
    /// </summary>
    public static async Task<(GeoPoint? Point, bool Failed)> ResolveZipAsync(
        IApplicationDbContext db, IZipGeocoder? geocoder, string zip5, ILogger? logger, CancellationToken ct)
    {
        var cached = await db.ZipGeocodes.AsNoTracking()
            .Where(z => z.Zip == zip5)
            .Select(z => new { z.Latitude, z.Longitude })
            .FirstOrDefaultAsync(ct);
        if (cached is not null)
            return (new GeoPoint(cached.Latitude, cached.Longitude), false);

        var failed = false;
        var configured = geocoder is not null && await geocoder.IsConfiguredAsync(ct);
        if (configured)
        {
            try
            {
                if (await geocoder!.GeocodeZipAsync(zip5, ct) is { } point)
                {
                    await CacheAsync(db, zip5, point, "Geocodio", ct);
                    return (point, false);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
            {
                failed = true;
                logger?.LogWarning(ex, "FFL: geocoding ZIP {Zip} failed; falling back to the dealer directory.", zip5);
            }
        }

        // Fallback: the directory already knows where its dealers are.
        var centroid = await db.FflDealers.AsNoTracking()
            .Where(d => d.PremiseZipCode != null && d.PremiseZipCode.StartsWith(zip5) && d.Latitude != null && d.Longitude != null)
            .GroupBy(_ => 1)
            .Select(g => new { Lat = g.Average(d => d.Latitude!.Value), Lon = g.Average(d => d.Longitude!.Value) })
            .FirstOrDefaultAsync(ct);
        if (centroid is not null)
        {
            var point = new GeoPoint(centroid.Lat, centroid.Lon);
            // Only remember it when no provider is set up; otherwise let the provider answer next time.
            if (!configured)
                await CacheAsync(db, zip5, point, "Directory", ct);
            return (point, false);
        }

        return (null, failed);
    }

    private static async Task CacheAsync(IApplicationDbContext db, string zip5, GeoPoint point, string source, CancellationToken ct)
    {
        try
        {
            db.ZipGeocodes.Add(new ZipGeocode { Zip = zip5, Latitude = point.Latitude, Longitude = point.Longitude, Source = source });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two searches for the same ZIP raced; the other one cached it.
        }
    }

    /// <summary>
    /// Radius search (FFL-31…35): dealers with a geocode, a transfer licence type, transfers not refused, an
    /// unexpired licence and within <paramref name="radius"/> miles of the ZIP centre, nearest first, capped at
    /// <paramref name="maxResults"/> (FFL-39). A bounding box narrows the rows before the haversine (FFL-131).
    /// Throws <see cref="ServiceUnavailableException"/> when the ZIP could not be geocoded because the provider failed.
    /// </summary>
    public static async Task<FflSearchResultDto> SearchAsync(
        IApplicationDbContext db, IZipGeocoder? geocoder, string zip5, int radius, int maxResults,
        DateTime todayUtc, ILogger? logger, CancellationToken ct)
    {
        var result = new FflSearchResultDto { Zip = zip5, Radius = radius };

        var (centre, failed) = await ResolveZipAsync(db, geocoder, zip5, logger, ct);
        if (centre is not { } c)
        {
            if (failed)
                throw new ServiceUnavailableException(FflMessages.SearchFailed);
            result.Message = FflMessages.NoResults(radius, zip5);
            return result;
        }

        var latDelta = radius / 69.0;
        var lonDelta = radius / (69.0 * Math.Max(0.05, Math.Cos(c.Latitude * Math.PI / 180.0)));
        double minLat = c.Latitude - latDelta, maxLat = c.Latitude + latDelta;
        double minLon = c.Longitude - lonDelta, maxLon = c.Longitude + lonDelta;

        var candidates = await db.FflDealers.AsNoTracking()
            .Where(d => d.IsActive
                        && d.Latitude != null && d.Longitude != null
                        && TransferLicenseTypes.Contains(d.LicType)
                        && d.AllowsTransfers != false
                        && d.Latitude >= minLat && d.Latitude <= maxLat
                        && d.Longitude >= minLon && d.Longitude <= maxLon)
            .ToListAsync(ct);

        var found = candidates
            .Where(d => !IsExpired(d, todayUtc))
            .Select(d => (Dealer: d, Distance: HaversineMiles(c.Latitude, c.Longitude, d.Latitude!.Value, d.Longitude!.Value)))
            .Where(x => x.Distance <= radius)
            .OrderBy(x => x.Distance)
            .ThenBy(x => DisplayName(x.Dealer))
            .ToList();

        result.TotalFound = found.Count;
        result.Truncated = found.Count > maxResults;
        result.Dealers = found.Take(maxResults).Select(x => ToDto(x.Dealer, todayUtc, x.Distance)).ToList();
        if (result.Dealers.Count == 0)
            result.Message = FflMessages.NoResults(radius, zip5);
        return result;
    }
}
