using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Persistence;
using HKPartsEcommerce.Infrastructure.Tax;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Tax;

/// <summary>
/// Tax on refunds (PHP EcommOrder::applyCredit / applyCreditAvalara): the tax share handed back, the Avalara return
/// routing, and the admin Refund carrying the lines' tax. DB-backed parts run against the SQL Server test DB.
/// </summary>
public class RefundTaxTests : CatalogTestBase
{
    // ---- OrderRefundTax (pure) ------------------------------------------------------------------------------

    private static Order Order(decimal tax, decimal refunded = 0m, decimal discount = 0m, decimal shipping = 0m)
    {
        var order = new Order { TaxTotal = tax, TaxRefundedTotal = refunded, DiscountTotal = discount, ShippingTotal = shipping };
        order.Lines.Add(new OrderLineItem { ProductName = "A", Quantity = 1, UnitPrice = 60m, LineTotal = 60m });
        order.Lines.Add(new OrderLineItem { ProductName = "B", Quantity = 2, UnitPrice = 20m, LineTotal = 40m });
        order.Subtotal = 100m;
        order.TotalAmount = 100m - discount + shipping + tax;
        return order;
    }

    [Fact]
    public void ForLines_ProRatesTheOrderTax()
        => Assert.Equal(4.00m, OrderRefundTax.ForLines(Order(tax: 10m), 40m));

    [Fact]
    public void ForLines_NeverExceedsTheTaxNotYetRefunded()
        => Assert.Equal(1.50m, OrderRefundTax.ForLines(Order(tax: 10m, refunded: 8.50m), 40m));

    [Fact]
    public void ForAmount_FullRefund_ReturnsAllRemainingTax()
        => Assert.Equal(7m, OrderRefundTax.ForAmount(Order(tax: 10m, refunded: 3m), 50m, full: true));

    [Fact]
    public void ForAmount_PartialRefund_IsProportionalToTheOrderTotal()
        // 55 of a 110 order (100 + 10 tax) → half the tax.
        => Assert.Equal(5m, OrderRefundTax.ForAmount(Order(tax: 10m), 55m, full: false));

    [Fact]
    public void LinesReturn_SendsEachLineNetOfTheOrderDiscount()
    {
        var order = Order(tax: 9m, discount: 10m);
        var b = order.Lines.Last();

        var req = OrderRefundTax.LinesReturn(order, new HashSet<Guid> { b.Id }, 3.60m, "Refund");

        var line = Assert.Single(req.Lines);
        Assert.Equal(36m, line.Amount); // 40 less the 10% order discount
        Assert.Equal(3.60m, req.TaxAmount);
        Assert.Equal(0m, req.ShippingAmount);
    }

    [Fact]
    public void ProportionalReturn_ScalesLinesAndShipping()
    {
        var order = Order(tax: 10m, shipping: 10m); // total 120
        var req = OrderRefundTax.ProportionalReturn(order, 60m, 5m, "Refund");

        Assert.Equal(5m, req.ShippingAmount);
        Assert.Equal(50m, req.Lines.Sum(l => l.Amount));
    }

    // ---- TaxCalculationService.ReturnTransactionAsync (routing) ---------------------------------------------

    private sealed class RecordingAvalara : ITaxProviderClient
    {
        public bool Fail { get; set; }
        public List<TaxReturnRequest> Returns { get; } = new();
        public TaxProviderType Provider => TaxProviderType.Avalara;
        public Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest req, CancellationToken ct) => throw new NotSupportedException();
        public Task ReportTransactionAsync(Guid orderId, CancellationToken ct) => Task.CompletedTask;
        public Task ReturnTransactionAsync(TaxReturnRequest request, CancellationToken ct)
        {
            if (Fail) throw new HttpRequestException("Avalara is down");
            Returns.Add(request);
            return Task.CompletedTask;
        }
    }

    private Guid SeedOrder(string? docCode, bool voided = false, decimal tax = 10m, bool withPayment = false)
    {
        var product = SeedProduct();
        using var db = NewContext();
        var order = new Order
        {
            OrderNumber = "ORD-TAX-" + Guid.NewGuid().ToString("N")[..8],
            Status = OrderStatus.Processing,
            PaymentStatus = PaymentStatus.Captured,
            CurrencyCode = "USD",
            PlacedOnUtc = DateTime.UtcNow,
            Subtotal = 100m,
            TaxTotal = tax,
            TotalAmount = 100m + tax,
            TaxDocumentCode = docCode,
            TaxDocumentVoidedOnUtc = voided ? DateTime.UtcNow : null,
        };
        order.Lines.Add(new OrderLineItem { ProductId = product, ProductName = "A", Quantity = 1, UnitPrice = 60m, LineTotal = 60m });
        order.Lines.Add(new OrderLineItem { ProductId = product, ProductName = "B", Quantity = 2, UnitPrice = 20m, LineTotal = 40m });
        db.Orders.Add(order);
        if (withPayment)
            db.PaymentRecords.Add(new PaymentRecord
            {
                OrderId = order.Id,
                Method = PaymentMethodType.Stripe,
                GatewayName = "Stripe",
                Amount = order.TotalAmount,
                CurrencyCode = "USD",
                Status = PaymentStatus.Captured,
            });
        db.SaveChanges();
        return order.Id;
    }

    private TaxCalculationService Service(ITaxProviderClient avalara, FakeAdminAlertService alerts) =>
        new(NewContext(), new[] { avalara }, new MemoryCache(new MemoryCacheOptions()), alerts, NullLogger<TaxCalculationService>.Instance);

    private static TaxReturnRequest Return(Guid orderId) => new(orderId, new() { new TaxReturnLine(null, 1, 40m) }, 0m, 4m, "Refund");

    [Fact]
    public async Task Return_OrderCommittedToAvalara_IsSentToAvalara()
    {
        var orderId = SeedOrder("ORD-1");
        var avalara = new RecordingAvalara();

        await Service(avalara, new FakeAdminAlertService()).ReturnTransactionAsync(Return(orderId));

        Assert.Single(avalara.Returns);
    }

    [Theory]
    [InlineData(false)] // never committed
    [InlineData(true)]  // committed, then voided
    public async Task Return_WithoutALiveDocument_IsNotSent(bool voided)
    {
        var orderId = SeedOrder(voided ? "ORD-2" : null, voided);
        var avalara = new RecordingAvalara();

        await Service(avalara, new FakeAdminAlertService()).ReturnTransactionAsync(Return(orderId));

        Assert.Empty(avalara.Returns);
    }

    [Fact]
    public async Task Return_ProviderFailure_RaisesAnAlertInsteadOfThrowing()
    {
        var orderId = SeedOrder("ORD-3");
        var alerts = new FakeAdminAlertService();

        await Service(new RecordingAvalara { Fail = true }, alerts).ReturnTransactionAsync(Return(orderId));

        Assert.Equal(1, alerts.Raised);
    }

    // ---- RefundOrder carries the lines' tax -----------------------------------------------------------------

    private static InventoryService NewInventory(AppDbContext db) => new(db, new FakeAdminAlertService(), new FakeNoopPublisher());

    [Fact]
    public async Task LineRefund_AddsTheLinesTax_AndRecordsIt()
    {
        var orderId = SeedOrder("ORD-4", withPayment: true);
        var lineB = NewContext().OrderLineItems.AsNoTracking().First(l => l.OrderId == orderId && l.ProductName == "B").Id;
        var sender = new FakeRefundSender();
        var avalara = new RecordingAvalara();

        using var db = NewContext();
        var handler = new RefundOrderCommandHandler(db, sender, new FakeEmailTemplateSender(), NewInventory(db),
            new FakeRewardPointsService(), Service(avalara, new FakeAdminAlertService()));
        await handler.Handle(new RefundOrderCommand(orderId, new() { lineB }, RestockItems: false), default);

        Assert.Equal(44m, sender.LastAmount); // 40 of goods + 4 of their tax
        Assert.Equal(4m, NewContext().Orders.AsNoTracking().First(o => o.Id == orderId).TaxRefundedTotal);
        var ret = Assert.Single(avalara.Returns);
        Assert.Equal(4m, ret.TaxAmount);
        Assert.Equal(lineB, Assert.Single(ret.Lines).OrderLineItemId);
    }

    [Fact]
    public async Task FullRefund_RecordsAllTheTax()
    {
        var orderId = SeedOrder(null, withPayment: true);
        var sender = new FakeRefundSender { FullRefundAmount = 110m };

        using var db = NewContext();
        var handler = new RefundOrderCommandHandler(db, sender, new FakeEmailTemplateSender(), NewInventory(db), new FakeRewardPointsService());
        await handler.Handle(new RefundOrderCommand(orderId, RestockItems: false), default);

        Assert.Null(sender.LastAmount);
        Assert.Equal(10m, NewContext().Orders.AsNoTracking().First(o => o.Id == orderId).TaxRefundedTotal);
    }
}
