using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Persistence;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Orders;

/// <summary>
/// The order stock lifecycle: the single stock-out path decrements a placed order's lines, and the single
/// restock path returns them on cancellation and refund. Backed by the real SQL Server test DB.
/// </summary>
public class OrderStockTests : CatalogTestBase
{
    // ---- Seed helpers ---------------------------------------------------------------------------

    private Guid SeedStore(string name = "Store")
    {
        using var db = NewContext();
        var store = new Store { Name = name };
        db.Stores.Add(store);
        db.SaveChanges();
        return store.Id;
    }

    private void SeedLevel(Guid productId, Guid storeId, int quantity, Guid? variantId = null)
    {
        using var db = NewContext();
        db.InventoryLevels.Add(new InventoryLevel
        {
            ProductId = productId,
            ProductVariantId = variantId,
            StoreId = storeId,
            StockQuantity = quantity,
        });
        db.SaveChanges();
    }

    private int Level(Guid productId, Guid storeId, Guid? variantId = null)
    {
        using var db = NewContext();
        return db.InventoryLevels
            .Single(l => l.ProductId == productId && l.StoreId == storeId && l.ProductVariantId == variantId)
            .StockQuantity;
    }

    /// <summary>Persists an order (one line) so the lifecycle handlers can load it; returns the order id.</summary>
    private Guid SeedOrder(
        Guid storeId, Guid productId, int quantity,
        OrderStatus status, PaymentStatus paymentStatus, bool withCapturedPayment = false)
    {
        using var db = NewContext();
        var order = new Order
        {
            OrderNumber = "ORD-TEST-" + Guid.NewGuid().ToString("N")[..8],
            Status = status,
            PaymentStatus = paymentStatus,
            AssignedStoreId = storeId,
            CurrencyCode = "USD",
            PlacedOnUtc = DateTime.UtcNow,
        };
        order.Lines.Add(new OrderLineItem
        {
            ProductId = productId,
            ProductName = "P",
            Quantity = quantity,
            UnitPrice = 10m,
            LineTotal = 10m * quantity,
        });
        db.Orders.Add(order);
        if (withCapturedPayment)
            db.PaymentRecords.Add(new PaymentRecord
            {
                OrderId = order.Id,
                Method = PaymentMethodType.Stripe,
                GatewayName = "Stripe",
                Amount = 10m * quantity,
                CurrencyCode = "USD",
                Status = PaymentStatus.Captured,
            });
        db.SaveChanges();
        return order.Id;
    }

    private static InventoryService NewInventory(AppDbContext db)
        => new(db, new FakeAdminAlertService(), new FakeNoopPublisher());

    // ---- DecrementForOrderAsync (the single stock-out path) -------------------------------------

    [Fact]
    public async Task DecrementForOrder_ReducesStock_ByLineQuantity()
    {
        var product = SeedProduct();
        var store = SeedStore();
        SeedLevel(product, store, 10);

        using var db = NewContext();
        var inventory = NewInventory(db);
        var order = new Order { AssignedStoreId = store };
        order.Lines.Add(new OrderLineItem { ProductId = product, Quantity = 3, ProductName = "P" });

        await inventory.DecrementForOrderAsync(order);

        Assert.Equal(7, Level(product, store));
    }

    [Fact]
    public async Task DecrementForOrder_WithNoAssignedStore_IsNoOp()
    {
        var product = SeedProduct();
        var store = SeedStore();
        SeedLevel(product, store, 10);

        using var db = NewContext();
        var inventory = NewInventory(db);
        var order = new Order { AssignedStoreId = null };
        order.Lines.Add(new OrderLineItem { ProductId = product, Quantity = 3, ProductName = "P" });

        await inventory.DecrementForOrderAsync(order);

        Assert.Equal(10, Level(product, store));
    }

    // ---- RestoreForOrderAsync (the single restock path) -----------------------------------------

    [Fact]
    public async Task RestoreForOrder_RestoresEveryLine_ByDefault()
    {
        var product = SeedProduct();
        var store = SeedStore();
        SeedLevel(product, store, 5);

        using var db = NewContext();
        var inventory = NewInventory(db);
        var order = new Order { AssignedStoreId = store };
        order.Lines.Add(new OrderLineItem { ProductId = product, Quantity = 4, ProductName = "P" });

        await inventory.RestoreForOrderAsync(order);

        Assert.Equal(9, Level(product, store));
    }

    [Fact]
    public async Task RestoreForOrder_WithLineSubset_RestoresOnlyThoseLines()
    {
        var productA = SeedProduct();
        var productB = SeedProduct();
        var store = SeedStore();
        SeedLevel(productA, store, 5);
        SeedLevel(productB, store, 5);

        using var db = NewContext();
        var inventory = NewInventory(db);
        var order = new Order { AssignedStoreId = store };
        var lineA = new OrderLineItem { ProductId = productA, Quantity = 2, ProductName = "A" };
        var lineB = new OrderLineItem { ProductId = productB, Quantity = 3, ProductName = "B" };
        order.Lines.Add(lineA);
        order.Lines.Add(lineB);

        await inventory.RestoreForOrderAsync(order, new[] { lineA.Id });

        Assert.Equal(7, Level(productA, store)); // restored
        Assert.Equal(5, Level(productB, store)); // untouched
    }

    // ---- Cancellation restocks (AdvanceOrderStatus → Cancelled) ---------------------------------

    [Fact]
    public async Task Cancel_PaidOrder_RestocksItsLines()
    {
        var product = SeedProduct();
        var store = SeedStore();
        SeedLevel(product, store, 8);
        var orderId = SeedOrder(store, product, 3, OrderStatus.Processing, PaymentStatus.Captured);

        using var db = NewContext();
        var handler = new AdvanceOrderStatusCommandHandler(
            db, new FakeEmailTemplateSender(), new FakeCurrentUser(),
            new FixedClock(DateTime.UtcNow), new FakePaymentRouter(), NewInventory(db));

        await handler.Handle(new AdvanceOrderStatusCommand(orderId, "Cancelled", null, null), default);

        Assert.Equal(11, Level(product, store));
    }

    [Fact]
    public async Task Cancel_CashOnDeliveryOrder_RestocksItsLines()
    {
        // COD/bank-transfer orders settle out-of-band (AwaitingPayment) but still commit stock at placement,
        // so cancelling one must restock — same as a card-paid order.
        var product = SeedProduct();
        var store = SeedStore();
        SeedLevel(product, store, 8);
        var orderId = SeedOrder(store, product, 3, OrderStatus.Processing, PaymentStatus.AwaitingPayment);

        using var db = NewContext();
        var handler = new AdvanceOrderStatusCommandHandler(
            db, new FakeEmailTemplateSender(), new FakeCurrentUser(),
            new FixedClock(DateTime.UtcNow), new FakePaymentRouter(), NewInventory(db));

        await handler.Handle(new AdvanceOrderStatusCommand(orderId, "Cancelled", null, null), default);

        Assert.Equal(11, Level(product, store));
    }

    [Fact]
    public async Task Cancel_UnpaidPendingOrder_DoesNotRestock()
    {
        var product = SeedProduct();
        var store = SeedStore();
        SeedLevel(product, store, 8);
        var orderId = SeedOrder(store, product, 3, OrderStatus.Pending, PaymentStatus.Pending);

        using var db = NewContext();
        var handler = new AdvanceOrderStatusCommandHandler(
            db, new FakeEmailTemplateSender(), new FakeCurrentUser(),
            new FixedClock(DateTime.UtcNow), new FakePaymentRouter(), NewInventory(db));

        await handler.Handle(new AdvanceOrderStatusCommand(orderId, "Cancelled", null, null), default);

        Assert.Equal(8, Level(product, store));
    }

    // ---- Refund restocks (RefundOrder), with the RMA double-restock guard -----------------------

    [Fact]
    public async Task Refund_FullOrder_RestocksItsLines()
    {
        var product = SeedProduct();
        var store = SeedStore();
        SeedLevel(product, store, 4);
        var orderId = SeedOrder(store, product, 2, OrderStatus.Processing, PaymentStatus.Captured, withCapturedPayment: true);

        using var db = NewContext();
        var handler = new RefundOrderCommandHandler(
            db, new FakeRefundSender(), new FakeEmailTemplateSender(), NewInventory(db), new FakeRewardPointsService());

        await handler.Handle(new RefundOrderCommand(orderId), default);

        Assert.Equal(6, Level(product, store));
    }

    [Fact]
    public async Task Refund_WithRestockDisabled_DoesNotRestock()
    {
        // Mirrors an RMA-driven refund: the return flow already restocked the received units, so the
        // refund must not restock them again (RestockItems: false).
        var product = SeedProduct();
        var store = SeedStore();
        SeedLevel(product, store, 4);
        var orderId = SeedOrder(store, product, 2, OrderStatus.Processing, PaymentStatus.Captured, withCapturedPayment: true);

        using var db = NewContext();
        var handler = new RefundOrderCommandHandler(
            db, new FakeRefundSender(), new FakeEmailTemplateSender(), NewInventory(db), new FakeRewardPointsService());

        await handler.Handle(new RefundOrderCommand(orderId, RestockItems: false), default);

        Assert.Equal(4, Level(product, store));
    }
}
