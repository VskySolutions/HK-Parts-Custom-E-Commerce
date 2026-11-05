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
/// The legacy site's rate methods missing until now (docs/shipping-requirements.md §2): size-based
/// "Custom" pricing with the FFL flat rate and USPS-abroad rule, flat rate per item, and "up to" tiers.
/// Case ids (T1…) are the spec's acceptance tests (§10.2).
/// </summary>
public class ShippingMethodsPhpParityTests : CatalogTestBase
{
    private const string LegacyTiers =
        "[{\"size\":\"Free\",\"rate\":0},{\"size\":\"Small\",\"rate\":9.50},{\"size\":\"Medium\",\"rate\":16.50}," +
        "{\"size\":\"Large\",\"rate\":22.50},{\"size\":\"Extra Large\",\"rate\":28.00}]";

    private static readonly CarrierAddress UsOrigin = new("US", "FL", "33101", null, null);
    private static readonly CarrierAddress UsDest = new("US", "FL", "33139", null, null);
    private static readonly CarrierAddress CanadaDest = new("CA", "ON", "M5V 2T6", null, null);

    private sealed class StubUsps : ICarrierClient
    {
        private readonly IReadOnlyList<ShippingRateOption> _rates;
        public StubUsps(params ShippingRateOption[] rates) => _rates = rates;
        public ShippingCarrierType Carrier => ShippingCarrierType.USPS;
        public string CarrierName => "USPS";
        public Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
            => Task.FromResult(_rates);
    }

    private static ShippingRateLine Line(string? size, bool ffl = false, int qty = 1, decimal total = 50m, bool giftCert = false)
        => new(Guid.NewGuid(), qty, total, ffl, giftCert, size, 2m, null, null, null, null, null, 1, false);

    private static CarrierRateRequest Req(CarrierAddress dest, params ShippingRateLine[] lines)
        => new(UsOrigin, dest, 1m, null, null, null, lines.Sum(l => l.LineTotal), lines);

    private void Seed(ShippingMethod method, bool uspsEnabled = false)
    {
        using var db = NewContext();
        var config = new ShippingProviderConfiguration { IsEnabled = true };
        config.Carriers.Add(new ShippingCarrierSetting { Carrier = ShippingCarrierType.Manual, IsEnabled = true });
        // USPS off as a rate source: the size-based method must still be able to ask it for the abroad rate.
        config.Carriers.Add(new ShippingCarrierSetting { Carrier = ShippingCarrierType.USPS, IsEnabled = uspsEnabled, DisplayOrder = 3 });
        db.ShippingProviderConfigurations.Add(config);
        method.IsEnabled = true;
        db.ShippingMethods.Add(method);
        db.SaveChanges();
    }

    private static ShippingMethod SizeBased(bool international = true) => new()
    {
        Name = "Standard Shipping",
        MethodType = ShippingMethodType.SizeBased,
        TiersJson = LegacyTiers,
        FlatRate = 9.50m,
        FflFlatRate = 40m,
        InternationalViaUsps = international,
    };

    private async Task<IReadOnlyList<ShippingRateOption>> RateAsync(CarrierRateRequest request, ICarrierClient? usps = null)
    {
        using var db = NewContext();
        var services = new ServiceCollection();
        var carriers = usps is null ? Array.Empty<ICarrierClient>() : new[] { usps };
        foreach (var c in carriers)
            services.AddScoped<ICarrierClient>(_ => c);
        using var provider = services.BuildServiceProvider();
        var service = new ShippingRateService(
            db, carriers, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ShippingRateService>.Instance);
        return await service.GetRatesAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task T1_highest_size_tier_wins()
    {
        Seed(SizeBased());
        var options = await RateAsync(Req(UsDest, Line("Small"), Line("Large")));
        Assert.Equal(22.50m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task T2_only_free_size_items_ship_free()
    {
        Seed(SizeBased());
        var options = await RateAsync(Req(UsDest, Line("Free"), Line("free")));
        Assert.Equal(0m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task Blank_or_unknown_size_uses_the_default_rate()
    {
        Seed(SizeBased());
        var options = await RateAsync(Req(UsDest, Line(null), Line("Huge")));
        Assert.Equal(9.50m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task T3_ffl_only_cart_is_the_flat_rate()
    {
        Seed(SizeBased());
        var options = await RateAsync(Req(UsDest, Line("Large", ffl: true)));
        Assert.Equal(40m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task T4_mixed_cart_adds_ffl_flat_to_size_tier()
    {
        Seed(SizeBased());
        var options = await RateAsync(Req(UsDest, Line(null, ffl: true), Line("Medium")));
        Assert.Equal(56.50m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task T12_us_address_uses_size_tier_not_usps()
    {
        Seed(SizeBased());
        var usps = new StubUsps(new ShippingRateOption("PMI", "USPS PMI", "USPS", null, 3m));
        var options = await RateAsync(Req(UsDest, Line("Medium")), usps);
        Assert.Equal(16.50m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task T8_abroad_uses_cheapest_usps_rate()
    {
        Seed(SizeBased());
        var usps = new StubUsps(
            new ShippingRateOption("PMEI", "USPS Priority Mail Express International", "USPS", null, 61.20m),
            new ShippingRateOption("PMI", "USPS Priority Mail International", "USPS", null, 38.40m));
        var options = await RateAsync(Req(CanadaDest, Line("Small")), usps);
        var option = Assert.Single(options);
        Assert.Equal(38.40m, option.Rate);
        Assert.Contains("International", option.Name);
    }

    [Fact]
    public async Task T9_abroad_with_usps_down_offers_nothing_rather_than_free()
    {
        Seed(SizeBased());
        var options = await RateAsync(Req(CanadaDest, Line("Small")), new StubUsps());
        Assert.Empty(options);
    }

    [Fact]
    public async Task Ffl_items_never_ship_abroad()
    {
        Seed(SizeBased());
        var usps = new StubUsps(new ShippingRateOption("PMI", "USPS PMI", "USPS", null, 30m));
        var options = await RateAsync(Req(CanadaDest, Line(null, ffl: true), Line("Small")), usps);
        Assert.Empty(options);
    }

    [Fact]
    public async Task Abroad_without_usps_rule_keeps_size_tiers()
    {
        Seed(SizeBased(international: false));
        var options = await RateAsync(Req(CanadaDest, Line("Large")));
        Assert.Equal(22.50m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task Flat_rate_per_item_multiplies_by_shippable_units()
    {
        Seed(new ShippingMethod { Name = "Per item", MethodType = ShippingMethodType.FlatRate, FlatRate = 3m, FlatRatePerItem = true });
        var options = await RateAsync(Req(UsDest, Line(null, qty: 2), Line(null, qty: 3), Line(null, qty: 4, giftCert: true)));
        Assert.Equal(15m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task Flat_rate_per_order_ignores_quantity()
    {
        Seed(new ShippingMethod { Name = "Per order", MethodType = ShippingMethodType.FlatRate, FlatRate = 3m });
        var options = await RateAsync(Req(UsDest, Line(null, qty: 5)));
        Assert.Equal(3m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task Up_to_tiers_from_the_admin_ui_are_honoured()
    {
        Seed(new ShippingMethod
        {
            Name = "By total",
            MethodType = ShippingMethodType.PriceBased,
            TiersJson = "[{\"upTo\":50,\"rate\":8},{\"upTo\":100,\"rate\":5}]",
        });
        var options = await RateAsync(Req(UsDest, Line(null, total: 75m)));
        Assert.Equal(5m, Assert.Single(options).Rate);
    }
}
