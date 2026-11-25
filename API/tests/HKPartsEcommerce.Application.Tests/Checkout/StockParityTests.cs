using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Application.Features.Inventory;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Application.Tests.GiftCertificates;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Routing;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Checkout;

/// <summary>
/// PHP parity for stock at checkout (legacy EcommCart::checkInventory + Settings → Catalog → "Use inventory"): one
/// stock number per product = the total over every store; a short item is refused by name; backorders and the
/// setting switched off let the order through; an order is never refused because no single store holds it all.
/// </summary>
public class StockParityTests : GiftCertificateTestBase
{
    private static string Session() => "s-" + Guid.NewGuid().ToString("N");

    private void SetStock(Guid productId, int qty)
    {
        using var db = NewContext();
        db.InventoryLevels.Where(l => l.ProductId == productId).ExecuteUpdate(s => s.SetProperty(l => l.StockQuantity, qty));
    }

    private async Task<CheckoutQuote> QuoteAs(string session)
    {
        await using var db = NewContext();
        return await Orchestrator(db).QuoteAsync(new CheckoutQuoteRequest(null, session, ShipTo, null), default);
    }

    [Fact]
    public async Task Item_short_of_stock_is_refused_by_name_like_the_old_site()
    {
        var product = SeedSellable(20m, "Sear spring");
        SetStock(product, 1);
        var session = Session();
        await AddToCart(session, product, qty: 1);
        await using (var db = NewContext())
            await db.CartItems.Where(i => i.ProductId == product).ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, 2));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => QuoteAs(session));
        Assert.Equal(CheckoutErrorCodes.OutOfStock, ex.Extensions?["code"]);
        Assert.Equal(TotalStockCheck.MessagePrefix + "Sear spring", ex.Message);
    }

    [Fact]
    public async Task Backorder_or_use_inventory_off_lets_the_order_through()
    {
        var product = SeedSellable(20m, "Sear spring");
        SetStock(product, 0);
        var session = Session();
        await AddToCart(session, product);

        await using (var db = NewContext())
            await db.Products.Where(p => p.Id == product).ExecuteUpdateAsync(s => s.SetProperty(p => p.AllowBackorder, true));
        Assert.True((await QuoteAs(session)).IsRoutable);

        await using (var db = NewContext())
        {
            await db.Products.Where(p => p.Id == product).ExecuteUpdateAsync(s => s.SetProperty(p => p.AllowBackorder, false));
            await Settings(db).SetAsync(CheckoutSettingsStore.UseInventoryKey, "false");
        }
        Assert.True((await QuoteAs(session)).IsRoutable);
    }

    [Fact]
    public async Task Stock_split_across_stores_routes_on_the_total_and_the_assigned_store_goes_below_zero()
    {
        var product = SeedProduct(p => { p.Name = "Sear spring"; p.IsPublished = true; });
        Guid near, far;
        using (var db = NewContext())
        {
            var a = new Store { Name = "A", IsEnabled = true };
            var b = new Store { Name = "B", IsEnabled = true };
            db.Stores.AddRange(a, b);
            db.InventoryLevels.Add(new InventoryLevel { ProductId = product, StoreId = a.Id, StockQuantity = 1 });
            db.InventoryLevels.Add(new InventoryLevel { ProductId = product, StoreId = b.Id, StockQuantity = 1 });
            db.SaveChanges();
            near = a.Id;
            far = b.Id;
        }

        await using (var db = NewContext())
        {
            var inventory = new InventoryService(db, new FakeAdminAlertService(), new FakeNoopPublisher());
            var engine = new OrderRoutingEngine(db, inventory, new FakeAdminAlertService(), new FakeNoopPublisher(), Settings(db));
            var result = await engine.RouteAsync(new RoutingRequest(null, null, "US", "TX", "78701",
                new List<RoutingLineItem> { new(product, null, 2) }), default);
            Assert.True(result.IsRouted); // 1 + 1 covers 2: the old site would sell it

            // Stock comes off the assigned store, which may go below zero; the product total ends at 0.
            var order = new Order { OrderNumber = "ORD-SPLIT", AssignedStoreId = result.AssignedStoreId, PlacedOnUtc = Now };
            order.Lines.Add(new OrderLineItem { ProductId = product, ProductName = "Sear spring", Quantity = 2, UnitPrice = 1m, LineTotal = 2m });
            await inventory.DecrementForOrderAsync(order, default);
        }
        await using (var db = NewContext())
        {
            // Whichever store took it (the test base adds a third), the product total across stores ends at 0.
            var levels = db.InventoryLevels.Where(l => l.ProductId == product).ToList();
            Assert.Equal(0, levels.Sum(l => l.StockQuantity));
            Assert.Contains(levels, l => l.StockQuantity < 0);
            Assert.Contains(levels, l => l.StoreId == near || l.StoreId == far);
        }
    }
}
