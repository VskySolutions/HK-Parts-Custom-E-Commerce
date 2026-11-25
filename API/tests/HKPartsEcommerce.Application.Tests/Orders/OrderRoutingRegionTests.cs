using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Infrastructure.Checkout;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Routing;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Orders;

/// <summary>
/// Routing against delivery zones: a US state matches whatever its spelling ("California" zone, "CA" address, as an
/// FFL dealer address sends it), and checkout names the real reason an order can't be routed.
/// </summary>
public class OrderRoutingRegionTests : CatalogTestBase
{
    private (Guid ProductId, Guid StoreId) Seed(string zoneRegion, int stock)
    {
        var productId = SeedProduct(p => { p.Name = "Sear spring"; p.IsPublished = true; });
        using var db = NewContext();
        var store = new Store { Name = "West", IsEnabled = true };
        store.DeliveryZones.Add(new DeliveryZone { Name = "CA only", CountryCode = "US", Region = zoneRegion, IsActive = true });
        db.Stores.Add(store);
        db.InventoryLevels.Add(new InventoryLevel { ProductId = productId, StoreId = store.Id, StockQuantity = stock });
        db.SaveChanges();
        return (productId, store.Id);
    }

    private async Task<RoutingResult> Route(Guid productId, string? region, int qty = 2)
    {
        await using var db = NewContext();
        var engine = new OrderRoutingEngine(db, new InventoryService(db, new FakeAdminAlertService(), new FakeNoopPublisher()),
            new FakeAdminAlertService(), new FakeNoopPublisher());
        return await engine.RouteAsync(new RoutingRequest(null, null, "US", region, "90001",
            new List<RoutingLineItem> { new(productId, null, qty) }), default);
    }

    [Theory]
    [InlineData("California", "CA")]
    [InlineData("CA", "California")]
    [InlineData("ca", "CA")]
    public async Task Zone_state_matches_whatever_its_spelling(string zone, string address)
    {
        var (productId, storeId) = Seed(zone, stock: 5);
        var result = await Route(productId, address);
        Assert.True(result.IsRouted);
        Assert.Equal(storeId, result.AssignedStoreId);
    }

    [Fact]
    public async Task Other_state_is_still_outside_the_zone_and_named_as_an_address_problem()
    {
        var (productId, _) = Seed("California", stock: 5);
        var result = await Route(productId, "TX");
        Assert.False(result.IsRouted);
        Assert.Contains("deliver to this address", CheckoutOrchestrator.UnroutableReason(result));
    }

    [Fact]
    public async Task Short_stock_is_named_as_a_stock_problem_not_an_address_problem()
    {
        var (productId, _) = Seed("CA", stock: 1);
        var result = await Route(productId, "CA", qty: 2);
        Assert.False(result.IsRouted);
        Assert.Contains("aren't in stock", CheckoutOrchestrator.UnroutableReason(result));
    }
}
