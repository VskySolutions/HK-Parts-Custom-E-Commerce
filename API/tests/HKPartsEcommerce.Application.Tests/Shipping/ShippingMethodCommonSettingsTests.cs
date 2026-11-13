using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.ShippingMethods;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Shipping;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Shipping;

/// <summary>
/// AD-04 on the custom methods: handling fee (added to the rate and carried apart for the order's shipping
/// row), minimum order amount and ship-to countries — the settings every carrier method already had.
/// </summary>
public class ShippingMethodCommonSettingsTests : CatalogTestBase
{
    private static CarrierRateRequest Request(string country = "US", decimal subtotal = 100m) => new(
        Origin: new CarrierAddress("US", "FL", "33101", null, null),
        Destination: new CarrierAddress(country, country == "US" ? "FL" : null, "33139", null, null),
        WeightKg: 1m, Length: null, Width: null, Height: null,
        OrderSubtotal: subtotal);

    private void Seed(Action<ShippingMethod> configure)
    {
        using var db = NewContext();
        var method = new ShippingMethod { Name = "Standard Shipping", MethodType = ShippingMethodType.FlatRate, FlatRate = 9.50m, IsEnabled = true };
        configure(method);
        db.ShippingMethods.Add(method);
        db.SaveChanges();
    }

    private async Task<IReadOnlyList<ShippingRateOption>> Rates(CarrierRateRequest request)
    {
        using var db = NewContext();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new ShippingRateService(
            db, Array.Empty<ICarrierClient>(), provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ShippingRateService>.Instance);
        return await service.GetRatesAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task Handling_fee_is_added_to_the_rate_and_carried_separately()
    {
        Seed(m => m.HandlingFee = 2.00m);

        var option = Assert.Single(await Rates(Request()));

        Assert.Equal(11.50m, option.Rate);
        Assert.Equal(2.00m, option.HandlingFee);
    }

    [Fact]
    public async Task Handling_fee_applies_to_free_shipping_too()
    {
        Seed(m => { m.MethodType = ShippingMethodType.FreeShipping; m.FlatRate = null; m.HandlingFee = 1.25m; });

        var option = Assert.Single(await Rates(Request()));

        Assert.Equal(1.25m, option.Rate);
    }

    [Fact]
    public async Task Method_below_its_minimum_order_amount_is_not_offered()
    {
        Seed(m => m.MinimumOrderAmount = 150m);

        Assert.Empty(await Rates(Request(subtotal: 149.99m)));
        Assert.Single(await Rates(Request(subtotal: 150m)));
    }

    [Fact]
    public async Task Method_is_offered_only_to_its_ship_to_countries()
    {
        Seed(m => m.ShipToCountries = "US,CA");

        Assert.Single(await Rates(Request("US")));
        Assert.Single(await Rates(Request("ca")));
        Assert.Empty(await Rates(Request("GB")));
    }

    [Fact]
    public async Task Create_and_update_save_the_common_settings()
    {
        await using var db = NewContext();
        var created = await new CreateShippingMethodCommandHandler(db).Handle(new CreateShippingMethodCommand(
            "Standard", ShippingMethodType.FlatRate, FlatRate: 9.50m,
            HandlingFee: 1.50m, MinimumOrderAmount: 25m, ShipToCountries: new List<string> { "us", " ca ", "US" }), default);

        Assert.Equal(1.50m, created.HandlingFee);
        Assert.Equal(25m, created.MinimumOrderAmount);
        Assert.Equal(new[] { "US", "CA" }, created.ShipToCountries);

        var updated = await new UpdateShippingMethodCommandHandler(db).Handle(new UpdateShippingMethodCommand(
            created.Id, "Standard", ShippingMethodType.FlatRate, FlatRate: 9.50m), default);

        Assert.Null(updated.HandlingFee);
        Assert.Null(updated.MinimumOrderAmount);
        Assert.Empty(updated.ShipToCountries);
    }
}
