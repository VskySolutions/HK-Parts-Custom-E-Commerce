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

            var rate = method.MethodType switch
            {
                ShippingMethodType.FlatRate => matchedZoneRate?.Rate ?? method.FlatRate,
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

        foreach (var tier in tiers.OrderBy(t => t.Min))
        {
            if (value >= tier.Min && value <= tier.Max)
                return tier.Rate;
        }

        return null;
    }

    /// <summary>A single weight/price tier row parsed from <see cref="ShippingMethod.TiersJson"/>.</summary>
    private sealed record ShippingTier(decimal Min, decimal Max, decimal Rate);
}
