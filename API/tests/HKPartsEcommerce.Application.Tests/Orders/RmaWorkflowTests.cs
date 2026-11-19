using System.Text.Json;
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
/// The returns (RMA) workflow end to end against the SQL Server test DB (docs/order-return-requirements.bak.md):
/// request → approve → receive (restock + audit) → inspect → refund, plus eligibility, failure and fee rules.
/// </summary>
public class RmaWorkflowTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly FixedClock _clock = new(Now);
    private readonly InMemorySettings _settings = new();
    private readonly FakeEmailTemplateSender _emails = new();
    private readonly StubRefundRouter _router = new();

    public RmaWorkflowTests()
    {
        ReturnSettingsStore.SaveAsync(_settings, new ReturnSettingsDto { Enabled = true, WindowDays = 30 }, default).GetAwaiter().GetResult();
    }

    // ---- seed ------------------------------------------------------------------------------------

    private sealed record Seeded(Guid OrderId, Guid LineId, Guid ProductId, Guid StoreId, Guid CustomerId, Guid UserId, Guid PaymentId);

    /// <summary>A delivered order: 2 × 50.00 (+10.00 shipping, +8.00 tax = 118.00) paid by Stripe, 5 in stock.</summary>
    private Seeded SeedOrder(DateTime? shippedOn = null, PaymentMethodType method = PaymentMethodType.Stripe)
    {
        var productId = SeedProduct(p => p.Name = "Brake pad");
        using var db = NewContext();
        var store = new Store { Name = "Main" };
        db.Stores.Add(store);
        db.InventoryLevels.Add(new InventoryLevel { ProductId = productId, StoreId = store.Id, StockQuantity = 5 });
        var user = new User { Username = $"u{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@test.local", PasswordHash = "x" };
        db.Users.Add(user);
        var customer = new Customer { UserId = user.Id, FirstName = "Ret", LastName = "Urner" };
        db.Customers.Add(customer);

        var order = new Order
        {
            OrderNumber = "ORD-RMA-" + Guid.NewGuid().ToString("N")[..8],
            CustomerId = customer.Id,
            Status = OrderStatus.Delivered,
            PaymentStatus = PaymentStatus.Captured,
            AssignedStoreId = store.Id,
            CurrencyCode = "USD",
            Subtotal = 100m,
            ShippingTotal = 10m,
            TaxTotal = 8m,
            TotalAmount = 118m,
            PlacedOnUtc = Now.AddDays(-10),
            ShippedOnUtc = shippedOn ?? Now.AddDays(-8),
            ShippingAddress = new Address { FirstName = "Ret", LastName = "Urner", Email = "buyer@test.local", AddressLine1 = "1 Main", City = "Town", PostalCode = "12345", CountryCode = "US" },
        };
        var line = new OrderLineItem { ProductId = productId, ProductName = "Brake pad", Sku = "BP-1", Quantity = 2, UnitPrice = 50m, LineTotal = 100m };
        order.Lines.Add(line);
        db.Orders.Add(order);
        var payment = new PaymentRecord
        {
            OrderId = order.Id, Method = method, GatewayName = method.ToString(), Amount = 118m, CurrencyCode = "USD",
            Status = PaymentStatus.Captured, TransactionId = "ch_1",
        };
        db.PaymentRecords.Add(payment);
        db.SaveChanges();
        return new Seeded(order.Id, line.Id, productId, store.Id, customer.Id, user.Id, payment.Id);
    }

    private Guid SeedReason(string name = "Doesn't fit", bool fee = false, bool fault = false)
    {
        using var db = NewContext();
        var reason = new RmaReason { Name = name + " " + Guid.NewGuid().ToString("N")[..6], FeeApplies = fee, MerchantFault = fault };
        db.RmaReasons.Add(reason);
        db.SaveChanges();
        return reason.Id;
    }

    // ---- handler factories ----------------------------------------------------------------------

    private FakeCurrentUser Customer(Seeded s) => new() { UserId = s.UserId, Email = "buyer@test.local" };
    private static FakeCurrentUser Admin() => new() { UserId = Guid.NewGuid(), Email = "staff@test.local" };

    private Task<RmaDto> SubmitAsync(AppDbContext db, Seeded s, int qty, Guid reasonId)
        => new SubmitRmaCommandHandler(db, Customer(s), _emails, _settings, new NullFileStore(), _clock)
            .Handle(new SubmitRmaCommand(s.OrderId, new() { new RmaRequestLine(s.LineId, qty, reasonId) }, AcceptPolicy: true), default);

    private Task<RmaDto> ApproveAsync(AppDbContext db, Guid id)
        => new ApproveRmaCommandHandler(db, Admin(), _settings, _emails, _clock).Handle(new ApproveRmaCommand(id), default);

    private Task<RmaDto> ReceiveAsync(AppDbContext db, Guid id, Guid lineId, int qty, RmaReceivedCondition condition = RmaReceivedCondition.Resellable)
        => new ReceiveRmaCommandHandler(db, Admin(), _settings, _emails, _clock, Inventory(db))
            .Handle(new ReceiveRmaCommand(id, new() { new RmaReceiveLine(lineId, qty, condition, RmaDisposition.Restock) }), default);

    private Task<RmaDto> InspectAsync(AppDbContext db, Guid id, Guid lineId, int accepted, bool refundShipping = false)
        => new InspectRmaCommandHandler(db, Admin(), _settings, _emails, _clock)
            .Handle(new InspectRmaCommand(id, new() { new RmaInspectLine(lineId, accepted) }, RefundShipping: refundShipping), default);

    private Task<RmaDto> RefundAsync(AppDbContext db, Guid id, RmaRefundMethod method = RmaRefundMethod.OriginalPayment)
        => new RefundRmaCommandHandler(db, Admin(), _settings, _emails, _clock,
                new RefundSender(db, _router, _clock), new StoreCreditService(db), new FakeRewardPointsService())
            .Handle(new RefundRmaCommand(id, method), default);

    private static InventoryService Inventory(AppDbContext db) => new(db, new FakeAdminAlertService(), new FakeNoopPublisher());

    // ---- tests ----------------------------------------------------------------------------------

    [Fact]
    public async Task FullFlow_RequestApproveReceiveInspectRefund_RefundsLinePlusTax_AndRestocksOnce()
    {
        var s = SeedOrder();
        var reason = SeedReason();
        using var db = NewContext();

        var rma = await SubmitAsync(db, s, 1, reason);
        Assert.Equal(RmaStatus.Requested, rma.Status);
        Assert.StartsWith("RMA-ORD-RMA-", rma.RmaNumber);
        Assert.Equal(54m, rma.Refund.Total); // estimate: 50 item + 4 tax
        Assert.True(rma.Refund.IsEstimate);

        rma = await ApproveAsync(db, rma.Id);
        Assert.Equal(RmaStatus.Approved, rma.Status);
        Assert.Equal(Now.AddDays(30), rma.LabelExpiresAtUtc);

        var lineId = rma.Lines.Single().Id;
        rma = await ReceiveAsync(db, rma.Id, lineId, 1);
        Assert.Equal(RmaStatus.Received, rma.Status);
        Assert.Equal(1, rma.Lines.Single().QuantityRestocked);

        // Re-saving the same receipt must not restock again.
        await ReceiveAsync(db, rma.Id, lineId, 1);

        rma = await InspectAsync(db, rma.Id, lineId, 1);
        Assert.Equal(RmaStatus.Inspected, rma.Status);
        Assert.Equal(54m, rma.Refund.Total);

        rma = await RefundAsync(db, rma.Id);
        Assert.Equal(RmaStatus.Refunded, rma.Status);
        Assert.Equal(54m, rma.RefundedAmount);
        Assert.Equal(RmaRefundMethod.OriginalPayment, rma.RefundMethod);

        using var check = NewContext();
        var payment = check.PaymentRecords.Single(p => p.Id == s.PaymentId);
        Assert.Equal(54m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(6, check.InventoryLevels.Single(l => l.ProductId == s.ProductId && l.StoreId == s.StoreId).StockQuantity);
        var audit = check.InventoryAuditLogs.Single(a => a.RmaId == rma.Id);
        Assert.Equal(InventoryAuditAction.ReturnRestock, audit.Action);
        Assert.Equal(5, audit.OldQty);
        Assert.Equal(6, audit.NewQty);
        Assert.True(check.RmaHistory.Count(h => h.RmaId == rma.Id) >= 5);
    }

    [Fact]
    public async Task Submit_CannotClaimUnitsAlreadyOnAnOpenReturn()
    {
        var s = SeedOrder();
        var reason = SeedReason();
        using var db = NewContext();

        await SubmitAsync(db, s, 1, reason);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => SubmitAsync(db, s, 2, reason));
        Assert.Contains("at most 1", string.Join(" ", ex.Errors.SelectMany(e => e.Value)));
    }

    [Fact]
    public async Task Submit_AfterReturnWindow_IsRefused_ButStaffCanOverrideWithReason()
    {
        var s = SeedOrder(shippedOn: Now.AddDays(-45));
        var reason = SeedReason();
        using var db = NewContext();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => SubmitAsync(db, s, 1, reason));
        Assert.Contains("return window closed", ex.Message);

        var create = new CreateRmaCommandHandler(db, Admin(), _emails, _settings, _clock);
        var lines = new List<RmaRequestLine> { new(s.LineId, 1, reason) };
        await Assert.ThrowsAsync<ValidationException>(() => create.Handle(new CreateRmaCommand(s.OrderId, lines), default));

        var rma = await create.Handle(new CreateRmaCommand(s.OrderId, lines, OverrideReason: "Loyal customer"), default);
        Assert.Equal(RmaSource.Admin, rma.Source);
        Assert.Equal("Loyal customer", rma.EligibilityOverrideReason);
    }

    [Fact]
    public async Task Refund_GatewayFailure_LeavesReturnInspected_ThenRetrySucceeds()
    {
        var s = SeedOrder();
        var reason = SeedReason();
        using var db = NewContext();
        var rma = await SubmitAsync(db, s, 2, reason);
        await ApproveAsync(db, rma.Id);
        var lineId = rma.Lines.Single().Id;
        await ReceiveAsync(db, rma.Id, lineId, 2);
        await InspectAsync(db, rma.Id, lineId, 2);

        _router.Fail = "Card network unavailable";
        await Assert.ThrowsAsync<ConflictException>(() => RefundAsync(db, rma.Id));

        using (var check = NewContext())
        {
            var saved = check.Rmas.Single(r => r.Id == rma.Id);
            Assert.Equal(RmaStatus.Inspected, saved.Status);
            Assert.Equal("Card network unavailable", saved.LastRefundError);
            Assert.Null(saved.RefundStartedOnUtc);
            Assert.Equal(0m, check.PaymentRecords.Single(p => p.Id == s.PaymentId).RefundedAmount);
        }

        _router.Fail = null;
        var done = await RefundAsync(db, rma.Id);
        Assert.Equal(RmaStatus.Refunded, done.Status);
        Assert.Equal(2, done.RefundAttempts);
        Assert.Equal(108m, done.RefundedAmount); // 100 items + 8 tax; shipping not refunded (not merchant fault)
    }

    [Fact]
    public async Task Refund_WholeOrderBackForMerchantFault_IncludesShipping()
    {
        var s = SeedOrder();
        var reason = SeedReason("Defective", fault: true);
        using var db = NewContext();
        var rma = await SubmitAsync(db, s, 2, reason);
        await ApproveAsync(db, rma.Id);
        var lineId = rma.Lines.Single().Id;
        await ReceiveAsync(db, rma.Id, lineId, 2, RmaReceivedCondition.Defective);
        rma = await InspectAsync(db, rma.Id, lineId, 2);

        Assert.Equal(10m, rma.Refund.Shipping);
        Assert.Equal(118m, rma.Refund.Total);
        // Defective units are accepted for the refund but never restocked.
        Assert.Equal(0, rma.Lines.Single().QuantityRestocked);
    }

    [Fact]
    public async Task Refund_CashOnDeliveryOrder_RequiresManualRefund()
    {
        var s = SeedOrder(method: PaymentMethodType.CashOnDelivery);
        var reason = SeedReason();
        using var db = NewContext();
        var rma = await SubmitAsync(db, s, 1, reason);
        await ApproveAsync(db, rma.Id);
        var lineId = rma.Lines.Single().Id;
        await ReceiveAsync(db, rma.Id, lineId, 1);
        await InspectAsync(db, rma.Id, lineId, 1);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => RefundAsync(db, rma.Id));
        Assert.Contains("manual refund", ex.Message);

        var manual = await new RefundRmaCommandHandler(db, Admin(), _settings, _emails, _clock,
                new RefundSender(db, _router, _clock), new StoreCreditService(db), new FakeRewardPointsService())
            .Handle(new RefundRmaCommand(rma.Id, RmaRefundMethod.Manual, "Cheque 1001"), default);
        Assert.Equal(RmaStatus.Refunded, manual.Status);
        Assert.Equal("Cheque 1001", manual.RefundReference);
    }

    [Fact]
    public async Task StoreCredit_IssuesTotalPlusBonus_ToCustomerBalance()
    {
        await ReturnSettingsStore.SaveAsync(_settings, new ReturnSettingsDto { Enabled = true, StoreCreditBonusPercent = 10m }, default);
        var s = SeedOrder();
        var reason = SeedReason();
        using var db = NewContext();
        var rma = await SubmitAsync(db, s, 1, reason);
        await ApproveAsync(db, rma.Id);
        var lineId = rma.Lines.Single().Id;
        await ReceiveAsync(db, rma.Id, lineId, 1);
        await InspectAsync(db, rma.Id, lineId, 1);

        var done = await RefundAsync(db, rma.Id, RmaRefundMethod.StoreCredit);
        Assert.Equal(RmaResolution.StoreCredit, done.Resolution);
        Assert.Equal(59.40m, done.StoreCreditIssued); // 54.00 + 10 %
        Assert.Equal(59.40m, await new StoreCreditService(db).GetBalanceAsync(s.CustomerId));
        Assert.Equal(0m, db.PaymentRecords.AsNoTracking().Single(p => p.Id == s.PaymentId).RefundedAmount);
    }

    [Fact]
    public async Task Customer_CanCancelRequestedReturn_AndUnitsBecomeReturnableAgain()
    {
        var s = SeedOrder();
        var reason = SeedReason();
        using var db = NewContext();
        var rma = await SubmitAsync(db, s, 2, reason);

        var cancelled = await new CancelMyRmaCommandHandler(db, Customer(s), _settings, _emails, _clock)
            .Handle(new CancelMyRmaCommand(rma.Id), default);
        Assert.Equal(RmaStatus.Cancelled, cancelled.Status);

        var again = await SubmitAsync(db, s, 2, reason);
        Assert.Equal(RmaStatus.Requested, again.Status);
        Assert.EndsWith("-2", again.RmaNumber);
    }

    [Fact]
    public async Task Workflow_RefusesTransitionOutsideTheDiagram()
    {
        var s = SeedOrder();
        var reason = SeedReason();
        using var db = NewContext();
        var rma = await SubmitAsync(db, s, 1, reason);

        // Requested → Inspected skips receipt.
        await Assert.ThrowsAsync<ConflictException>(() => InspectAsync(db, rma.Id, rma.Lines.Single().Id, 1));
    }

    [Fact]
    public void Calculator_AppliesRestockFeeOnlyToFeeReasons_AndProRatesOrderDiscount()
    {
        var order = new Order { Subtotal = 100m, DiscountTotal = 20m, ShippingTotal = 0m, TaxTotal = 0m, TotalAmount = 80m };
        var a = new OrderLineItem { Quantity = 1, LineTotal = 50m, ProductName = "A" };
        var b = new OrderLineItem { Quantity = 1, LineTotal = 50m, ProductName = "B" };
        order.Lines.Add(a);
        order.Lines.Add(b);

        var rma = new Domain.Entities.Rma { Status = RmaStatus.Inspected };
        rma.Lines.Add(new RmaLineItem { OrderLineItemId = a.Id, Quantity = 1, QuantityAccepted = 1, FeeApplies = true });
        rma.Lines.Add(new RmaLineItem { OrderLineItemId = b.Id, Quantity = 1, QuantityAccepted = 1, FeeApplies = true, MerchantFault = true });

        var settings = new ReturnSettingsDto { RestockFeePercent = 15m };
        var result = RmaRefundCalculator.Calculate(rma, order, settings, new Dictionary<Guid, int>(), 0m, persist: true);

        Assert.Equal(80m, result.Subtotal);        // 20 % order discount spread over both lines
        Assert.Equal(6m, result.RestockFee);       // 15 % of line A only (B is merchant fault)
        Assert.Equal(74m, result.Total);
        Assert.Equal(40m, rma.Lines.First().LineRefund);
    }

    // ---- doubles --------------------------------------------------------------------------------

    private sealed class InMemorySettings : ISettingsService
    {
        private readonly Dictionary<string, string?> _values = new();
        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(_values.GetValueOrDefault(key));
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_values.GetValueOrDefault(key) is string s ? JsonSerializer.Deserialize<T>(s) : default);
        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class NullFileStore : IPrivateFileStore
    {
        public Task<string> SaveAsync(string folder, string extension, Stream content, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult($"{folder}/{Guid.NewGuid():N}{extension}");
        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Gateway stub: refunds succeed unless <see cref="Fail"/> holds an error message.</summary>
    private sealed class StubRefundRouter : IPaymentGatewayRouter
    {
        public string? Fail { get; set; }

        public Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default)
            => Task.FromResult(Fail is null
                ? new PaymentResult(true, PaymentStatus.PartiallyRefunded, TransactionId: "re_" + Guid.NewGuid().ToString("N")[..8])
                : new PaymentResult(false, payment.Status, ErrorMessage: Fail));

        public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CaptureForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PaymentMethodAvailability>> AvailableMethodsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>ISender that runs the real <see cref="RefundPaymentCommandHandler"/> over the stub gateway.</summary>
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
