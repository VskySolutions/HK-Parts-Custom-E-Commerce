using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Checkout;

/// <summary>
/// Covers which stores a buyer may collect from. Two layers have to agree: the platform switch on the
/// singleton shipping configuration, and each store's own opt-in.
/// </summary>
public class ListPickupStoresTests : CatalogTestBase
{
    private Guid SeedStore(string name, bool isEnabled, bool pickupEnabled)
    {
        using var db = NewContext();
        var store = new Store
        {
            Name = name,
            IsEnabled = isEnabled,
            PickupEnabled = pickupEnabled,
            ContactEmail = "store@example.com",
        };
        db.Stores.Add(store);
        db.SaveChanges();
        return store.Id;
    }

    private void SeedConfiguration(bool pickupEnabled)
    {
        using var db = NewContext();
        db.ShippingProviderConfigurations.Add(new ShippingProviderConfiguration { PickupEnabled = pickupEnabled });
        db.SaveChanges();
    }

    private async Task<List<PickupStoreDto>> ListAsync()
    {
        using var db = NewContext();
        var handler = new ListPickupStoresQueryHandler(db);
        return await handler.Handle(new ListPickupStoresQuery(), CancellationToken.None);
    }

    [Fact]
    public async Task Lists_only_stores_that_opted_in()
    {
        SeedConfiguration(pickupEnabled: true);
        SeedStore("Collects", isEnabled: true, pickupEnabled: true);
        SeedStore("Delivery only", isEnabled: true, pickupEnabled: false);

        var stores = await ListAsync();

        Assert.Equal("Collects", Assert.Single(stores).Name);
    }

    [Fact]
    public async Task Disabled_store_is_never_offered()
    {
        SeedConfiguration(pickupEnabled: true);
        SeedStore("Closed", isEnabled: false, pickupEnabled: true);

        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task Platform_switch_off_withdraws_every_store()
    {
        // The kill switch has to beat the store's own opt-in, or turning collection off platform-wide would
        // silently do nothing.
        SeedConfiguration(pickupEnabled: false);
        SeedStore("Collects", isEnabled: true, pickupEnabled: true);

        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task No_configuration_row_leaves_pickup_available()
    {
        // An install that has never opened the configuration page must behave as though the switch is on,
        // exactly like the rate sources do.
        SeedStore("Collects", isEnabled: true, pickupEnabled: true);

        Assert.Single(await ListAsync());
    }
}
