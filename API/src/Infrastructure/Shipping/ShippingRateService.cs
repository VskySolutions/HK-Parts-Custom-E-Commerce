using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Common;

namespace HKPartsEcommerce.Infrastructure.Shipping;

/// <summary>
/// Aggregates shipping options for a shipment (WO-40): first the enabled custom shipping methods
/// (flat / weight / price / free, with per-zone rates and zone eligibility), then every live carrier the
/// admin has enabled, queried in parallel. Carriers that throw or return nothing are silently excluded so a
/// single failing integration never blocks checkout (AC-SHP-001.3).
///
/// Which sources take part is driven by <see cref="ShippingProviderConfiguration"/>: unlike tax, which
/// resolves exactly one active provider, shipping fans out to every enabled source and offers the union.
/// </summary>
public class ShippingRateService : IShippingRateService
{
    private static readonly JsonSerializerOptions TierJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<ICarrierClient> _carriers;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShippingRateService> _logger;

    public ShippingRateService(
        IApplicationDbContext db,
        IEnumerable<ICarrierClient> carriers,
        IServiceScopeFactory scopeFactory,
        ILogger<ShippingRateService> logger)
    {
        _db = db;
        _carriers = carriers;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var config = await LoadConfigurationAsync(ct);
        if (!config.IsEnabled)
            return Array.Empty<ShippingRateOption>();

        var options = new List<ShippingRateOption>();

        if (IsCarrierEnabled(config, ShippingCarrierType.Manual))
            options.AddRange(await EvaluateCustomMethodsAsync(request, ct));

        // Query each enabled carrier concurrently, in the admin's display order; a carrier that throws or
        // returns nothing is silently excluded from the aggregate (AC-SHP-001.3).
        var carriers = _carriers
            .Where(c => IsCarrierEnabled(config, c.Carrier))
            .OrderBy(c => DisplayOrderOf(config, c.Carrier))
            .ToList();

        var carrierResults = await Task.WhenAll(carriers.Select(c => SafeGetRatesAsync(c.Carrier, request, ct)));
        foreach (var result in carrierResults)
            options.AddRange(result);

        return options;
    }

    /// <summary>
    /// Loads the singleton configuration with its carrier rows. A missing row means the admin has never
    /// visited the configuration tab, so every source stays on — matching the pre-configuration behaviour
    /// rather than silently disabling shipping on upgrade.
    /// </summary>
    private async Task<ShippingProviderConfiguration> LoadConfigurationAsync(CancellationToken ct)
        => await _db.ShippingProviderConfigurations
            .AsNoTracking()
            .Include(c => c.Carriers)
            .FirstOrDefaultAsync(ct)
           ?? DefaultConfiguration();

    private static ShippingProviderConfiguration DefaultConfiguration()
    {
        var config = new ShippingProviderConfiguration { IsEnabled = true };
        foreach (var carrier in Enum.GetValues<ShippingCarrierType>())
            config.Carriers.Add(new ShippingCarrierSetting { Carrier = carrier, IsEnabled = true, DisplayOrder = (int)carrier });
        return config;
    }

    /// <summary>A carrier with no row is treated as disabled (AC-SHP-005.2).</summary>
    private static bool IsCarrierEnabled(ShippingProviderConfiguration config, ShippingCarrierType carrier)
        => config.Carriers.FirstOrDefault(s => s.Carrier == carrier)?.IsEnabled == true;

    private static int DisplayOrderOf(ShippingProviderConfiguration config, ShippingCarrierType carrier)
        => config.Carriers.FirstOrDefault(s => s.Carrier == carrier)?.DisplayOrder ?? int.MaxValue;

    private async Task<IReadOnlyList<ShippingRateOption>> EvaluateCustomMethodsAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var methods = await _db.ShippingMethods
            .AsNoTracking()
            .Include(m => m.ZoneRates)
                .ThenInclude(r => r.ShippingZone)
            .Where(m => m.IsEnabled)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(ct);

        var destination = request.Destination;
        var results = new List<ShippingRateOption>();

        foreach (var method in methods)
        {
            var matchedZoneRate = method.ZoneRates.FirstOrDefault(zr => ZoneMatches(zr.ShippingZone, destination));

            // Zone eligibility (AC-SHP-003.6): a method that carries any per-zone rate is only offered
            // when the destination falls inside one of those zones (hide out-of-zone).
            if (method.ZoneRates.Count > 0 && matchedZoneRate is null)
                continue;

            if (method.MethodType == ShippingMethodType.SizeBased)
            {
                var sized = await EvaluateSizeBasedAsync(method, request, ct);
                if (sized is not null)
                    results.Add(sized);
                continue;
            }

            var rate = method.MethodType switch
            {
                ShippingMethodType.FlatRate => FlatRateFor(method, matchedZoneRate?.Rate ?? method.FlatRate, request),
                ShippingMethodType.FreeShipping =>
                    request.OrderSubtotal >= (method.FreeShippingThreshold ?? 0m) ? 0m : (decimal?)null,
                ShippingMethodType.WeightBased => PickTierRate(method.TiersJson, request.WeightKg),
                ShippingMethodType.PriceBased => PickTierRate(method.TiersJson, request.OrderSubtotal),
                _ => null,
            };

            if (rate is null)
                continue;

            results.Add(new ShippingRateOption(
                MethodId: method.Id.ToString(),
                Name: method.Name,
                Carrier: "Custom",
                EstimatedDeliveryDays: method.TransitDays,
                Rate: rate.Value));
        }

        return results;
    }

    /// <summary>
    /// Flat rate per order, or — when <see cref="ShippingMethod.FlatRatePerItem"/> — per shippable unit, gift
    /// certificates excluded. Per-item needs the cart lines; without them the method is not offered rather
    /// than guessed at.
    /// </summary>
    private static decimal? FlatRateFor(ShippingMethod method, decimal? rate, CarrierRateRequest request)
    {
        if (rate is null || !method.FlatRatePerItem)
            return rate;
        if (request.Lines is null)
            return null;

        var units = request.Lines.Where(l => !l.IsGiftCertificate).Sum(l => l.Quantity);
        return units == 0 ? null : rate.Value * units;
    }

    /// <summary>
    /// The legacy "Custom" method. Domestic: the highest size-tier price among the cart's non-FFL items (an
    /// item with no size, or a size not in the list, costs <see cref="ShippingMethod.FlatRate"/>), plus
    /// <see cref="ShippingMethod.FflFlatRate"/> once when the cart holds FFL items. International (destination
    /// country differs from the origin): the non-FFL part is the cheapest USPS international service when
    /// <see cref="ShippingMethod.InternationalViaUsps"/> is on, and the method is withheld when USPS quotes
    /// nothing — never offered free. FFL items cannot leave the country, so a non-US cart holding them gets
    /// no option from this method.
    /// </summary>
    private async Task<ShippingRateOption?> EvaluateSizeBasedAsync(
        ShippingMethod method, CarrierRateRequest request, CancellationToken ct)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            return null;

        var tiers = ParseSizeTiers(method.TiersJson);
        var defaultRate = method.FlatRate ?? 0m;

        // Split by flag, not by Except: lines are value-equal records, and Except would also de-duplicate them.
        var splitFfl = method.FflFlatRate.HasValue;
        var fflLines = request.Lines.Where(l => splitFfl && l.IsFfl).ToList();
        var otherLines = request.Lines.Where(l => !(splitFfl && l.IsFfl)).ToList();

        var international = IsInternational(request.Origin, request.Destination);
        if (international && request.Lines.Any(l => l.IsFfl))
            return null;

        var name = method.Name;
        decimal otherRate = 0m;
        if (otherLines.Count > 0)
        {
            if (international && method.InternationalViaUsps)
            {
                var usps = await CheapestUspsAsync(request, otherLines, ct);
                if (usps is null)
                {
                    _logger.LogWarning(
                        "Shipping method {Method}: USPS returned no international rate for {Country}; the method is withheld.",
                        method.Name, request.Destination.CountryCode);
                    return null;
                }
                otherRate = usps.Rate;
                name = $"International Shipping ({usps.Name})";
            }
            else
            {
                otherRate = otherLines.Max(l =>
                    l.ShippingSize is { } size && tiers.TryGetValue(size.Trim(), out var tierRate) ? tierRate : defaultRate);
            }
        }

        var fflRate = fflLines.Count > 0 ? method.FflFlatRate!.Value : 0m;

        return new ShippingRateOption(
            MethodId: method.Id.ToString(),
            Name: name,
            Carrier: "Custom",
            EstimatedDeliveryDays: method.TransitDays,
            Rate: otherRate + fflRate);
    }

    /// <summary>Rates just the given lines with USPS (international) and returns the cheapest service.</summary>
    private async Task<ShippingRateOption?> CheapestUspsAsync(
        CarrierRateRequest request, IReadOnlyList<ShippingRateLine> lines, CancellationToken ct)
    {
        const decimal KgPerPound = 0.45359237m;
        var weightLb = lines.Sum(l => (l.WeightLb ?? 0m) * l.Quantity);
        var weightKg = weightLb > 0m ? weightLb * KgPerPound : request.WeightKg;
        var subtotal = lines.Sum(l => l.LineTotal);

        var uspsRequest = request with { WeightKg = weightKg, OrderSubtotal = subtotal, Lines = lines };
        var rates = await SafeGetRatesAsync(ShippingCarrierType.USPS, uspsRequest, ct);
        return rates.Where(r => r.Rate > 0m).OrderBy(r => r.Rate).FirstOrDefault();
    }

    private static bool IsInternational(CarrierAddress origin, CarrierAddress destination)
    {
        static string Norm(string? c) => string.IsNullOrWhiteSpace(c) ? "US"
            : c.Trim().ToUpperInvariant() is "USA" ? "US" : c.Trim().ToUpperInvariant();
        return Norm(origin.CountryCode) != Norm(destination.CountryCode);
    }

    /// <summary>Parses [{"size":"Small","rate":9.50}] into a case-insensitive size → rate map.</summary>
    private static Dictionary<string, decimal> ParseSizeTiers(string? json)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return map;
        try
        {
            var tiers = JsonSerializer.Deserialize<List<SizeTier>>(json, TierJsonOptions) ?? new();
            foreach (var t in tiers.Where(t => !string.IsNullOrWhiteSpace(t.Size)))
                map[t.Size!.Trim()] = t.Rate;
        }
        catch (JsonException)
        {
            // A malformed tier list prices every item at the default rate rather than failing the quote.
        }
        return map;
    }

    private sealed record SizeTier(string? Size, decimal Rate);

    /// <summary>
    /// Rates one carrier inside its own DI scope. Every client opens by resolving its credentials through
    /// the scoped <see cref="IApplicationDbContext"/>, and EF's DbContext is not thread-safe — so sharing
    /// this request's context across the fan-out above makes the carriers race, and whichever lose throw
    /// "A second operation was started on this context instance". A scope each gives every client its own
    /// context, keeping the parallelism this method exists for.
    /// </summary>
    private async Task<IReadOnlyList<ShippingRateOption>> SafeGetRatesAsync(
        ShippingCarrierType carrier, CarrierRateRequest request, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetServices<ICarrierClient>().First(c => c.Carrier == carrier);

            var rates = await client.GetRatesAsync(request, ct);

            // An enabled carrier that quotes nothing is indistinguishable at checkout from one that is
            // switched off, and silently drops the buyer's only delivery option — say so.
            if (rates.Count == 0)
                _logger.LogInformation(
                    "Shipping carrier {Carrier} returned no rates for {Country}/{PostalCode}.",
                    carrier, request.Destination.CountryCode, request.Destination.PostalCode);

            return rates;
        }
        catch (Exception ex)
        {
            // Exclude a failing carrier from the aggregate rather than fail the whole quote — but never
            // silently: unlogged, a bad credential looks exactly like a carrier with nothing to offer.
            _logger.LogWarning(
                ex, "Shipping carrier {Carrier} failed to return rates and was excluded from the quote.",
                carrier);
            return Array.Empty<ShippingRateOption>();
        }
    }

    /// <summary>True when a zone is enabled and its country / region / postal-range contains the destination.</summary>
    private static bool ZoneMatches(ShippingZone? zone, CarrierAddress destination)
    {
        if (zone is null || !zone.IsEnabled)
            return false;
        if (string.IsNullOrWhiteSpace(destination.CountryCode))
            return false;
        if (!string.Equals(zone.CountryCode, destination.CountryCode, StringComparison.OrdinalIgnoreCase))
            return false;
        // Compare the region as a normalized code rather than as typed. The admin defining a zone and the
        // buyer filling in the address form are describing the same place in different words — the form
        // stores "California" while a zone is usually keyed "CA" — and a raw string compare silently
        // withholds every method in the zone from every address in it.
        if (!string.IsNullOrWhiteSpace(zone.Region) &&
            !string.Equals(
                RegionCodeNormalizer.ToStateCode(zone.CountryCode, zone.Region),
                RegionCodeNormalizer.ToStateCode(destination.CountryCode, destination.Region),
                StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(zone.PostalCodeStart) &&
            !string.IsNullOrWhiteSpace(zone.PostalCodeEnd) &&
            !string.IsNullOrWhiteSpace(destination.PostalCode))
        {
            if (string.Compare(destination.PostalCode, zone.PostalCodeStart, StringComparison.OrdinalIgnoreCase) < 0 ||
                string.Compare(destination.PostalCode, zone.PostalCodeEnd, StringComparison.OrdinalIgnoreCase) > 0)
                return false;
        }
        return true;
    }

    /// <summary>Selects the first tier whose [Min,Max] range contains the value (weight or subtotal).</summary>
    private static decimal? PickTierRate(string? tiersJson, decimal value)
    {
        if (string.IsNullOrWhiteSpace(tiersJson))
            return null;

        List<ShippingTier>? tiers;
        try
        {
            tiers = JsonSerializer.Deserialize<List<ShippingTier>>(tiersJson, TierJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (tiers is null)
            return null;

        // The admin UI saves "up to" brackets ([{"upTo":5,"rate":..}]); each then starts just above the
        // previous one. Explicit {"min","max"} rows are honoured as written.
        if (tiers.Any(t => t.UpTo.HasValue))
        {
            foreach (var tier in tiers.Where(t => t.UpTo.HasValue).OrderBy(t => t.UpTo))
            {
                if (value <= tier.UpTo!.Value)
                    return tier.Rate;
            }
            return null;
        }

        foreach (var tier in tiers.OrderBy(t => t.Min))
        {
            if (value >= (tier.Min ?? 0m) && value <= (tier.Max ?? decimal.MaxValue))
                return tier.Rate;
        }

        return null;
    }

    /// <summary>A single weight/price tier row parsed from <see cref="ShippingMethod.TiersJson"/>.</summary>
    private sealed record ShippingTier(decimal? Min, decimal? Max, decimal? UpTo, decimal Rate);
}
