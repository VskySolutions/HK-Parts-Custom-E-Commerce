using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Shipping;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Shipping;

/// <summary>
/// Covers which rate sources <see cref="ShippingRateService"/> fans out to. Unlike tax, shipping offers the
/// union of every enabled source, so the gating is per-source rather than a single active provider.
/// </summary>
public class ShippingRateServiceTests : CatalogTestBase
{
    private static readonly CarrierRateRequest Request = new(
        Origin: new CarrierAddress("US", "Florida", "33101", null, null),
        Destination: new CarrierAddress("US", "Florida", "33139", null, null),
        WeightKg: 1m,
        Length: null, Width: null, Height: null,
        OrderSubtotal: 100m);

    /// <summary>A carrier that always quotes, so a missing option means it was never queried.</summary>
    private sealed class StubCarrier : ICarrierClient
    {
        public StubCarrier(ShippingCarrierType carrier) => Carrier = carrier;

        public ShippingCarrierType Carrier { get; }
        public string CarrierName => Carrier.ToString();
        public bool WasQueried { get; private set; }

        public Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
        {
            WasQueried = true;
            IReadOnlyList<ShippingRateOption> options = new[]
            {
                new ShippingRateOption($"{Carrier}-1", $"{Carrier} Standard", CarrierName, 3, 12.50m),
            };
            return Task.FromResult(options);
        }
    }

    private Guid SeedFlatRateMethod(string name = "Store pickup", decimal rate = 5m)
    {
        using var db = NewContext();
        var method = new ShippingMethod
        {
            Name = name,
            MethodType = ShippingMethodType.FlatRate,
            FlatRate = rate,
            IsEnabled = true,
        };
        db.ShippingMethods.Add(method);
        db.SaveChanges();
        return method.Id;
    }

    /// <summary>Records the DbContext its own scope handed it, so one shared across the fan-out is visible.</summary>
    private sealed class ContextCapturingCarrier : ICarrierClient
    {
        private readonly IApplicationDbContext _db;
        private readonly List<IApplicationDbContext> _seen;

        public ContextCapturingCarrier(ShippingCarrierType carrier, IApplicationDbContext db, List<IApplicationDbContext> seen)
        {
            Carrier = carrier;
            _db = db;
            _seen = seen;
        }

        public ShippingCarrierType Carrier { get; }
        public string CarrierName => Carrier.ToString();

        public Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
        {
            lock (_seen) _seen.Add(_db);
            IReadOnlyList<ShippingRateOption> options = new[]
            {
                new ShippingRateOption($"{Carrier}-1", $"{Carrier} Standard", CarrierName, 3, 12.50m),
            };
            return Task.FromResult(options);
        }
    }

    private void SeedConfiguration(bool isEnabled, params (ShippingCarrierType Carrier, bool Enabled)[] carriers)
    {
        using var db = NewContext();
        var config = new ShippingProviderConfiguration { IsEnabled = isEnabled };
        foreach (var (carrier, enabled) in carriers)
            config.Carriers.Add(new ShippingCarrierSetting { Carrier = carrier, IsEnabled = enabled, DisplayOrder = (int)carrier });
        db.ShippingProviderConfigurations.Add(config);
        db.SaveChanges();
    }

    private Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(params StubCarrier[] carriers)
        => GetRatesAsync(Request, carriers);

    private async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(
        CarrierRateRequest request, params StubCarrier[] carriers)
    {
        using var db = NewContext();

        // The service rates each carrier in its own DI scope (a shared DbContext across the concurrent
        // fan-out is not thread-safe), so it resolves the clients from the container rather than the
        // injected list — the stubs have to be registered for it to find them.
        var services = new ServiceCollection();
        foreach (var carrier in carriers)
            services.AddScoped<ICarrierClient>(_ => carrier);
        using var provider = services.BuildServiceProvider();

        var service = new ShippingRateService(
            db, carriers, provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ShippingRateService>.Instance);
        return await service.GetRatesAsync(request, CancellationToken.None);
    }

    /// <summary>A US shipment into <paramref name="region"/>, written however the caller writes it.</summary>
    private static CarrierRateRequest DestinationIn(string region) => Request with
    {
        Destination = new CarrierAddress("US", region, "93722", null, null),
    };

    /// <summary>
    /// A flat-rate method carrying one per-zone rate, so it is only offered where the zone matches.
    /// <paramref name="zoneRegion"/> is what the admin typed into the zone.
    /// </summary>
    private void SeedZonedFlatRateMethod(string zoneRegion, decimal zoneRate = 7.50m)
    {
        using var db = NewContext();
        var zone = new ShippingZone
        {
            Name = $"Zone {zoneRegion}",
            CountryCode = "US",
            Region = zoneRegion,
            IsEnabled = true,
        };
        var method = new ShippingMethod
        {
            Name = "Zoned flat",
            MethodType = ShippingMethodType.FlatRate,
            // Distinct from zoneRate so the assertion proves the zone rate won, not the fallback.
            FlatRate = 99m,
            IsEnabled = true,
        };
        method.ZoneRates.Add(new ShippingMethodZoneRate { ShippingZone = zone, Rate = zoneRate });
        db.ShippingZones.Add(zone);
        db.ShippingMethods.Add(method);
        db.SaveChanges();
    }

    [Fact]
    public async Task No_configuration_row_leaves_every_source_enabled()
    {
        SeedFlatRateMethod();
        var fedex = new StubCarrier(ShippingCarrierType.FedEx);

        var options = await GetRatesAsync(fedex);

        // An install that has never opened the config tab must keep quoting exactly as before.
        Assert.True(fedex.WasQueried);
        Assert.Contains(options, o => o.Carrier == "Custom");
        Assert.Contains(options, o => o.Carrier == "FedEx");
    }

    [Fact]
    public async Task Disabled_carrier_is_never_queried()
    {
        SeedConfiguration(isEnabled: true,
            (ShippingCarrierType.Manual, true),
            (ShippingCarrierType.FedEx, false),
            (ShippingCarrierType.UPS, true));

        var fedex = new StubCarrier(ShippingCarrierType.FedEx);
        var ups = new StubCarrier(ShippingCarrierType.UPS);

        var options = await GetRatesAsync(fedex, ups);

        Assert.False(fedex.WasQueried);
        Assert.True(ups.WasQueried);
        Assert.All(options, o => Assert.Equal("UPS", o.Carrier));
    }

    [Fact]
    public async Task Multiple_enabled_carriers_are_all_offered()
    {
        SeedConfiguration(isEnabled: true,
            (ShippingCarrierType.Manual, true),
            (ShippingCarrierType.FedEx, true),
            (ShippingCarrierType.UPS, true),
            (ShippingCarrierType.USPS, true));

        SeedFlatRateMethod();

        var options = await GetRatesAsync(
            new StubCarrier(ShippingCarrierType.FedEx),
            new StubCarrier(ShippingCarrierType.UPS),
            new StubCarrier(ShippingCarrierType.USPS));

        Assert.Equal(
            new[] { "Custom", "FedEx", "UPS", "USPS" },
            options.Select(o => o.Carrier).OrderBy(c => c).ToArray());
    }

    [Fact]
    public async Task Disabling_manual_suppresses_custom_methods_only()
    {
        SeedConfiguration(isEnabled: true,
            (ShippingCarrierType.Manual, false),
            (ShippingCarrierType.FedEx, true));

        SeedFlatRateMethod();

        var options = await GetRatesAsync(new StubCarrier(ShippingCarrierType.FedEx));

        Assert.DoesNotContain(options, o => o.Carrier == "Custom");
        Assert.Contains(options, o => o.Carrier == "FedEx");
    }

    [Fact]
    public async Task Master_switch_off_quotes_nothing()
    {
        SeedConfiguration(isEnabled: false,
            (ShippingCarrierType.Manual, true),
            (ShippingCarrierType.FedEx, true));

        SeedFlatRateMethod();
        var fedex = new StubCarrier(ShippingCarrierType.FedEx);

        var options = await GetRatesAsync(fedex);

        Assert.Empty(options);
        Assert.False(fedex.WasQueried);
    }

    [Fact]
    public async Task Carrier_with_no_setting_row_is_treated_as_disabled()
    {
        // Manual + FedEx configured; UPS predates nothing and simply has no row.
        SeedConfiguration(isEnabled: true,
            (ShippingCarrierType.Manual, true),
            (ShippingCarrierType.FedEx, true));

        var ups = new StubCarrier(ShippingCarrierType.UPS);

        var options = await GetRatesAsync(new StubCarrier(ShippingCarrierType.FedEx), ups);

        Assert.False(ups.WasQueried);
        Assert.DoesNotContain(options, o => o.Carrier == "UPS");
    }

    [Fact]
    public async Task Carriers_are_queried_in_display_order()
    {
        SeedConfiguration(isEnabled: true,
            (ShippingCarrierType.Manual, false),
            (ShippingCarrierType.FedEx, true),
            (ShippingCarrierType.UPS, true));

        // UPS is ordered ahead of FedEx.
        using (var db = NewContext())
        {
            var settings = db.ShippingCarrierSettings.ToList();
            settings.First(s => s.Carrier == ShippingCarrierType.UPS).DisplayOrder = 0;
            settings.First(s => s.Carrier == ShippingCarrierType.FedEx).DisplayOrder = 1;
            db.SaveChanges();
        }

        var options = await GetRatesAsync(
            new StubCarrier(ShippingCarrierType.FedEx),
            new StubCarrier(ShippingCarrierType.UPS));

        Assert.Equal(new[] { "UPS", "FedEx" }, options.Select(o => o.Carrier).ToArray());
    }

    /// <summary>
    /// The admin keying a zone and the buyer filling in the address form describe the same state in
    /// different words — the form stores "California", a zone is usually keyed "CA" — and comparing them
    /// raw withheld every method in the zone from every address in it, surfacing at checkout as "no
    /// delivery options are available for this address".
    /// </summary>
    [Theory]
    [InlineData("CA")]          // zone keyed by code
    [InlineData("California")]  // zone keyed by name
    [InlineData("california")]  // zone keyed carelessly
    public async Task Zone_matches_a_destination_whose_region_is_written_differently(string zoneRegion)
    {
        SeedConfiguration(isEnabled: true, (ShippingCarrierType.Manual, true));
        SeedZonedFlatRateMethod(zoneRegion);

        // What checkout actually sends: AppAddressForm stores the state's display name.
        var options = await GetRatesAsync(DestinationIn("California"));

        var option = Assert.Single(options);
        Assert.Equal(7.50m, option.Rate);
    }

    [Fact]
    public async Task Zone_for_another_region_still_excludes_the_method()
    {
        SeedConfiguration(isEnabled: true, (ShippingCarrierType.Manual, true));
        SeedZonedFlatRateMethod("NY");

        var options = await GetRatesAsync(DestinationIn("California"));

        // Normalizing the comparison must not make every zone match everything.
        Assert.Empty(options);
    }

    [Fact]
    public async Task Method_with_no_zone_rates_is_offered_everywhere()
    {
        // The universal fallback: a method carrying no per-zone rate skips zone eligibility entirely, so a
        // store with no zones set up still quotes something rather than stranding checkout.
        SeedConfiguration(isEnabled: true, (ShippingCarrierType.Manual, true));
        SeedFlatRateMethod("Standard Shipping", 9.99m);

        var options = await GetRatesAsync(DestinationIn("California"));

        var option = Assert.Single(options);
        Assert.Equal(9.99m, option.Rate);
    }

    [Fact]
    public async Task Each_carrier_is_rated_with_its_own_db_context()
    {
        // Every real client opens by resolving its credentials through the scoped DbContext, and EF's
        // DbContext is not thread-safe. Sharing this request's context across the concurrent fan-out made
        // the carriers race, and whichever lost threw "A second operation was started on this context
        // instance" and was silently dropped from the quote. Asserting the contexts are distinct pins the
        // fix without depending on winning a timing race.
        SeedConfiguration(isEnabled: true,
            (ShippingCarrierType.Manual, false),
            (ShippingCarrierType.FedEx, true),
            (ShippingCarrierType.UPS, true));

        var seen = new List<IApplicationDbContext>();
        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => NewContext());
        services.AddScoped<ICarrierClient>(sp => new ContextCapturingCarrier(
            ShippingCarrierType.FedEx, sp.GetRequiredService<IApplicationDbContext>(), seen));
        services.AddScoped<ICarrierClient>(sp => new ContextCapturingCarrier(
            ShippingCarrierType.UPS, sp.GetRequiredService<IApplicationDbContext>(), seen));
        using var provider = services.BuildServiceProvider();

        using var db = NewContext();
        using var listScope = provider.CreateScope();
        var service = new ShippingRateService(
            db,
            listScope.ServiceProvider.GetServices<ICarrierClient>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ShippingRateService>.Instance);

        var options = await service.GetRatesAsync(Request, CancellationToken.None);

        // Both carriers survive, and neither shared a context with the other.
        Assert.Equal(2, options.Count);
        Assert.Equal(2, seen.Count);
        Assert.Equal(2, seen.Distinct().Count());
    }
}
