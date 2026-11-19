using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Payments;
using HKPartsEcommerce.Application.Features.Rma;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Customers;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Persistence;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Orders;

/// <summary>
/// "Apply credit" — the PHP admin's credit (EcommOrder::applyCredit): edits the order's quantities and totals,
/// restocks, refunds the difference and logs an order note. SQL Server test DB.
/// </summary>
public class ApplyCreditTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly FixedClock _clock = new(Now);
    private readonly InMemorySettings _settings = new();
    private readonly StubRefundRouter _router = new();

    private sealed record Seeded(Guid OrderId, Guid LineId, Guid ProductId, Guid StoreId, Guid PaymentId);

    /// <summary>A paid order: 2 × 50.00 (+10.00 shipping, +8.00 tax = 118.00), 5 in stock.</summary>
    private Seeded SeedOrder(PaymentMethodType method = PaymentMethodType.Stripe, PaymentStatus status = PaymentStatus.Captured)
    {
        var productId = SeedProduct(p => p.Name = "Brake pad");
        using var db = NewContext();
        var store = new Store { Name = "Main" };
        db.Stores.Add(store);
        db.InventoryLevels.Add(new InventoryLevel { ProductId = productId, StoreId = store.Id, StockQuantity = 5 });
        var user = new User { Username = $"u{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@test.local", PasswordHash = "x" };
        db.Users.Add(user);
        var customer = new Customer { UserId = user.Id, FirstName = "Cre", LastName = "Dit" };
        db.Customers.Add(customer);

        var order = new Order
        {
            OrderNumber = "ORD-CR-" + Guid.NewGuid().ToString("N")[..8],
            CustomerId = customer.Id,
            Status = OrderStatus.Delivered,
            PaymentStatus = status,
            AssignedStoreId = store.Id,
            CurrencyCode = "USD",
            Subtotal = 100m,
            ShippingTotal = 10m,
            TaxTotal = 8m,
            TotalAmount = 118m,
            PlacedOnUtc = Now.AddDays(-10),
            ShippingAddress = new Address { FirstName = "Cre", LastName = "Dit", Email = "buyer@test.local", AddressLine1 = "1 Main", City = "Town", PostalCode = "12345", CountryCode = "US" },
        };
        var line = new OrderLineItem { ProductId = productId, ProductName = "Brake pad", Sku = "BP-1", Quantity = 2, UnitPrice = 50m, OriginalUnitPrice = 50m, LineTotal = 100m };
        order.Lines.Add(line);
        db.Orders.Add(order);
        var payment = new PaymentRecord
        {
            OrderId = order.Id, Method = method, GatewayName = method.ToString(), Amount = 118m, CurrencyCode = "USD",
            Status = status, TransactionId = "ch_1",
        };
        db.PaymentRecords.Add(payment);
        db.SaveChanges();
        return new Seeded(order.Id, line.Id, productId, store.Id, payment.Id);
    }

    private Task<QuickReturnResultDto> ApplyAsync(AppDbContext db, ApplyQuickReturnCommand cmd)
        => new ApplyQuickReturnCommandHandler(
                new RefundSender(db, _router, _clock), db, _settings,
                new InventoryService(db, new FakeAdminAlertService(), new FakeNoopPublisher()),
                new StoreCreditService(db), new FakeRewardPointsService(),
                new FakeCurrentUser { UserId = Guid.NewGuid(), Email = "staff@test.local" }, _clock)
            .Handle(cmd, default);

    [Fact]
    public async Task Returning_one_item_lowers_the_order_restocks_refunds_and_logs_a_note()
    {
        var s = SeedOrder();
        using var db = NewContext();

        // Return 1 of 2 to stock, shipping unchanged: credit 50 + tax 4 = 54.
        var result = await ApplyAsync(db, new ApplyQuickReturnCommand(s.OrderId,
            new() { new QuickReturnLine(s.LineId, 1, ReturnToStock: true) }, ShippingAmount: 10m));

        Assert.True(result.Success);
        Assert.Equal(54m, result.CreditAmount);
        Assert.Equal(54m, result.RefundedToPayment);
        Assert.Equal(0m, result.ManualRefundDue);

        using var check = NewContext();
        var order = await check.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == s.OrderId);
        var line = order.Lines.Single();
        Assert.Equal(1, line.Quantity);
        Assert.Equal(1, line.QuantityRefunded);
        Assert.Equal(50m, line.LineTotal);
        Assert.Equal(50m, order.Subtotal);
        Assert.Equal(4m, order.TaxTotal);
        Assert.Equal(10m, order.ShippingTotal);
        Assert.Equal(64m, order.TotalAmount);

        Assert.Equal(6, (await check.InventoryLevels.SingleAsync(i => i.ProductId == s.ProductId && i.StoreId == s.StoreId)).StockQuantity);
        Assert.Equal(54m, (await check.PaymentRecords.SingleAsync(p => p.Id == s.PaymentId)).RefundedAmount);
        var note = await check.OrderStatusHistory.Where(h => h.OrderId == s.OrderId).Select(h => h.Note).SingleAsync();
        Assert.Contains("Refund applied in the amount of USD 54.00", note);
        Assert.Contains("Returned 1 of SKU: BP-1", note);
    }

    [Fact]
    public async Task Lowering_only_shipping_credits_the_difference_and_does_not_restock_unticked_items()
    {
        var s = SeedOrder();
        using var db = NewContext();

        // Return 2 not to stock, and shipping 10 → 0: credit 100 + 10 + 8 = 118 (everything).
        var result = await ApplyAsync(db, new ApplyQuickReturnCommand(s.OrderId,
            new() { new QuickReturnLine(s.LineId, 2, ReturnToStock: false) }, ShippingAmount: 0m));

        Assert.Equal(118m, result.CreditAmount);
        using var check = NewContext();
        var order = await check.Orders.SingleAsync(o => o.Id == s.OrderId);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        Assert.Equal(5, (await check.InventoryLevels.SingleAsync(i => i.ProductId == s.ProductId)).StockQuantity);
    }

    [Fact]
    public async Task Nothing_returned_and_shipping_unchanged_means_no_credit_needed()
    {
        var s = SeedOrder();
        using var db = NewContext();
        var result = await ApplyAsync(db, new ApplyQuickReturnCommand(s.OrderId, new(), ShippingAmount: 10m));

        Assert.False(result.Success);
        Assert.Equal("No credit needed.", result.Message);
        using var check = NewContext();
        Assert.Equal(118m, (await check.Orders.SingleAsync(o => o.Id == s.OrderId)).TotalAmount);
    }

    [Fact]
    public async Task Bank_transfer_order_is_adjusted_and_the_refund_is_left_for_manual_action()
    {
        var s = SeedOrder(PaymentMethodType.BankTransfer);
        using var db = NewContext();
        var result = await ApplyAsync(db, new ApplyQuickReturnCommand(s.OrderId,
            new() { new QuickReturnLine(s.LineId, 1) }, ShippingAmount: 10m));

        Assert.Equal(54m, result.CreditAmount);
        Assert.Equal(0m, result.RefundedToPayment);
        Assert.Equal(54m, result.ManualRefundDue);
        Assert.Contains("manually refund", result.Message);
        using var check = NewContext();
        Assert.Equal(64m, (await check.Orders.SingleAsync(o => o.Id == s.OrderId)).TotalAmount);
    }

    [Fact]
    public async Task Gateway_refusal_keeps_the_order_adjustment_and_reports_the_error()
    {
        var s = SeedOrder();
        _router.Fail = "Card declined";
        using var db = NewContext();
        var result = await ApplyAsync(db, new ApplyQuickReturnCommand(s.OrderId,
            new() { new QuickReturnLine(s.LineId, 1) }, ShippingAmount: 10m));

        Assert.False(result.Success);
        Assert.Equal(54m, result.ManualRefundDue);
        Assert.Contains("could not be processed automatically", result.Message);
        using var check = NewContext();
        Assert.Equal(1, (await check.OrderLineItems.SingleAsync(l => l.Id == s.LineId)).Quantity);
    }

    [Fact]
    public async Task Returning_more_than_is_on_the_line_is_rejected()
    {
        var s = SeedOrder();
        using var db = NewContext();
        await Assert.ThrowsAsync<ValidationException>(() => ApplyAsync(db, new ApplyQuickReturnCommand(s.OrderId,
            new() { new QuickReturnLine(s.LineId, 3) }, ShippingAmount: 10m)));
    }

    // ---- fakes (same as RmaWorkflowTests) ---------------------------------------------------------------

    private sealed class InMemorySettings : ISettingsService
    {
        private readonly Dictionary<string, string?> _values = new();
        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(_values.GetValueOrDefault(key));
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_values.GetValueOrDefault(key) is string s ? System.Text.Json.JsonSerializer.Deserialize<T>(s) : default);
        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class StubRefundRouter : IPaymentGatewayRouter
    {
        public string? Fail { get; set; }

        public Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default)
            => Task.FromResult(Fail is null
                ? new PaymentResult(true, payment.Amount - payment.RefundedAmount - amount <= 0 ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded,
                    TransactionId: "re_" + Guid.NewGuid().ToString("N")[..8])
                : new PaymentResult(false, payment.Status, ErrorMessage: Fail));

        public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CaptureForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PaymentMethodAvailability>> AvailableMethodsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class RefundSender : ISender
    {
        private readonly AppDbContext _db;
        private readonly IPaymentGatewayRouter _router;
        private readonly IDateTimeProvider _clock;

        public RefundSender(AppDbContext db, IPaymentGatewayRouter router, IDateTimeProvider clock)
        {
            _db = db;
            _router = router;
            _clock = clock;
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is RefundPaymentCommand refund)
                return (TResponse)(object)await new RefundPaymentCommandHandler(_db, _router, _clock).Handle(refund, cancellationToken);
            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
