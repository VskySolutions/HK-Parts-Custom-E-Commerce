using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Cart;
using HKPartsEcommerce.Application.Features.GiftCertificates;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Checkout;
using HKPartsEcommerce.Infrastructure.Customers;
using HKPartsEcommerce.Infrastructure.Email;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Persistence;
using HKPartsEcommerce.Infrastructure.Pricing;
using HKPartsEcommerce.Infrastructure.Settings;

namespace HKPartsEcommerce.Application.Tests.GiftCertificates;

/// <summary>
/// SQL-backed harness for gift certificates: the real certificate service, mailer, settings, cart handlers and
/// checkout orchestrator, with only the outside world (routing, carriers, tax provider, payment gateways) faked.
/// Shipping and tax default to 0 so totals are exactly the product prices unless a test sets them.
/// </summary>
public abstract class GiftCertificateTestBase : CatalogTestBase
{
    protected static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    protected readonly HKPartsEcommerce.Application.Common.Interfaces.IDateTimeProvider Clock = new FixedClock(Now);
    protected readonly MemoryCache Cache = new(new MemoryCacheOptions());
    protected readonly ScriptedPayments Payments = new();
    protected readonly FakeTax Tax = new();
    protected decimal ShippingRate { get; set; }
    protected readonly Guid StoreId;
    protected readonly Guid TaxCategoryId;

    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Storefront:PublicBaseUrl"] = "https://shop.test" })
        .Build();

    protected GiftCertificateTestBase()
    {
        using var db = NewContext();
        var store = new Store { Name = "HK Parts", IsEnabled = true, BankTransferEnabled = true, CashOnDeliveryEnabled = true };
        db.Stores.Add(store);
        db.SaveChanges();
        StoreId = store.Id;
        TaxCategoryId = SeedTaxCategory();
    }

    // ---- seeding ------------------------------------------------------------------------------------------

    protected Guid SeedSellable(decimal price, string name = "Brake pads")
    {
        var id = SeedProduct(p => { p.Name = name; p.Sku = "SKU-" + Guid.NewGuid().ToString("N")[..6]; p.Price = price; p.IsPublished = true; }, TaxCategoryId);
        using var db = NewContext();
        db.InventoryLevels.Add(new InventoryLevel { ProductId = id, StoreId = StoreId, StockQuantity = 100 });
        db.SaveChanges();
        return id;
    }

    protected Guid SeedGiftCertificateProduct(decimal price, bool online = true) =>
        SeedProduct(p =>
        {
            p.Name = online ? "eGift Certificate" : "Gift Card (mailed)";
            p.ProductType = ProductType.GiftCard;
            p.Price = price;
            p.IsPublished = true;
            p.GiftCertificateOnline = online;
        }, TaxCategoryId);

    protected GiftCertificateService Service(AppDbContext db, ICurrentUserService? user = null) =>
        new(db, Settings(db), user ?? new FakeCurrentUser { Email = "admin@test" }, Clock,
            new GiftCertificateMailer(db, new EmailEnqueuer(db, Clock), Config), new FakeAdminAlertService(),
            NullLogger<GiftCertificateService>.Instance);

    protected SettingsService Settings(AppDbContext db) => new(db, Cache, new FakeCurrentUser(), Clock);

    protected async Task<GiftCertificate> SeedCertificate(
        decimal amount, Action<GiftCertificate>? configure = null)
    {
        await using var db = NewContext();
        var gc = await Service(db).CreateAsync(new GiftCertificateCreateRequest(
            StoreId, amount, GiftCertificateStatus.Active, GiftCertificateSource.Admin, true,
            null, null, "Robin", "robin@example.com", null, null, "seed"));
        if (configure is not null)
        {
            configure(gc);
            await db.SaveChangesAsync();
        }

        return gc;
    }

    protected decimal Balance(Guid gcId)
    {
        using var db = NewContext();
        return db.GiftCertificates.IgnoreQueryFiltersSafe().Single(g => g.Id == gcId).CurrentBalance;
    }

    protected List<GiftCertificateTransaction> Ledger(Guid gcId)
    {
        using var db = NewContext();
        return db.GiftCertificateTransactions.Where(t => t.GiftCertificateId == gcId).OrderBy(t => t.CreatedOnUtc).ThenBy(t => t.Action).ToList();
    }

    // ---- cart ---------------------------------------------------------------------------------------------

    protected ICustomerGroupService Groups(AppDbContext db) =>
        new CustomerGroupService(db, new FakeCurrentUser(), new PriceResolver(db, Clock));

    protected async Task<CartDto> AddToCart(string session, Guid productId, int qty = 1, bool gcDetails = false)
    {
        await using var db = NewContext();
        var handler = new AddItemCommandHandler(db, new FakeCurrentUser(), Groups(db), new FakeCommerce());
        return await handler.Handle(new AddItemCommand(productId, null, qty, session,
            GcSenderName: gcDetails ? "Sam Sender" : null,
            GcSenderEmail: gcDetails ? "sam@example.com" : null,
            GcRecipientName: gcDetails ? "Rita Recipient" : null,
            GcRecipientEmail: gcDetails ? "rita@example.com" : null,
            GcMessage: gcDetails ? "Happy birthday!" : null), default);
    }

    protected async Task<CartGiftCertificateDto> ApplyToCart(string session, string code)
    {
        await using var db = NewContext();
        return await new ApplyCartGiftCertificateHandler(db, new FakeCurrentUser(), Groups(db), new FakeCommerce(), Service(db), Settings(db))
            .Handle(new ApplyCartGiftCertificateCommand(code, session), default);
    }

    protected async Task<CartDto> GetCart(string session)
    {
        await using var db = NewContext();
        return await new GetCartQueryHandler(db, new FakeCurrentUser(), Groups(db), new FakeCommerce()).Handle(new GetCartQuery(session), default);
    }

    // ---- checkout -----------------------------------------------------------------------------------------

    protected static readonly CheckoutAddress ShipTo = new("Alice", "Buyer", "alice@example.com", "1 Main St", null, "Austin", "TX", "78701", "US");

    protected CheckoutOrchestrator Orchestrator(AppDbContext db) => new(
        db, new FakeRouting(StoreId), new FakeShipping(() => ShippingRate), new FakeSelector(), new DiscountService(db),
        new FakeCoupons(), Tax, Payments, new FakeCurrentUser(), Clock, new FakeTemplates(), new FakeNoopPublisher(),
        Groups(db), new InventoryService(db, new FakeAdminAlertService(), new FakeNoopPublisher()), new FakeRewardPointsService(),
        new FakeCommerce(), Service(db), Settings(db));

    protected async Task<CheckoutQuote> Quote(string session)
    {
        await using var db = NewContext();
        return await Orchestrator(db).QuoteAsync(new CheckoutQuoteRequest(null, session, ShipTo, null), default);
    }

    protected async Task<CheckoutResult> Place(string session, PaymentMethodType method = PaymentMethodType.Stripe, bool payWithGc = false)
    {
        await using var db = NewContext();
        return await Orchestrator(db).PlaceAsync(new PlaceCheckoutRequest(
            null, session, ShipTo, null, method, "tok_test", null, PayWithGiftCertificate: payWithGc), default);
    }

    protected Order LoadOrder(Guid orderId)
    {
        using var db = NewContext();
        return db.Orders.IgnoreQueryFiltersSafe()
            .Where(o => o.Id == orderId)
            .Select(o => o)
            .Single();
    }

    protected List<PaymentRecord> PaymentsOf(Guid orderId)
    {
        using var db = NewContext();
        return db.PaymentRecords.Where(p => p.OrderId == orderId).OrderBy(p => p.Method).ToList();
    }

    // ---- fakes --------------------------------------------------------------------------------------------

    protected sealed class ScriptedPayments : IPaymentGatewayRouter
    {
        public bool Fail { get; set; }
        public List<PaymentRequest> Authorizations { get; } = new();
        public List<(Guid PaymentId, decimal Amount)> Refunds { get; } = new();

        public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default)
        {
            Authorizations.Add(request);
            if (Fail) return Task.FromResult(new PaymentResult(false, PaymentStatus.Failed, ErrorMessage: "Card declined"));
            return Task.FromResult(request.Method is PaymentMethodType.BankTransfer or PaymentMethodType.CashOnDelivery
                ? new PaymentResult(true, PaymentStatus.AwaitingPayment)
                : new PaymentResult(true, PaymentStatus.Captured, TransactionId: "txn_" + Authorizations.Count));
        }

        public Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default) =>
            Task.FromResult(new PaymentResult(true, PaymentStatus.Captured, TransactionId: "cap_1"));

        public Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default)
        {
            Refunds.Add((payment.Id, amount));
            return Task.FromResult(new PaymentResult(true, PaymentStatus.Refunded));
        }

        public Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct = default) =>
            Task.FromResult(new PaymentResult(true, PaymentStatus.Captured));

        public Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct = default) =>
            Task.FromResult(new PaymentResult(true, PaymentStatus.Captured));

        public Task CaptureForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PaymentMethodAvailability>> AvailableMethodsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PaymentMethodAvailability>>(new[]
            {
                new PaymentMethodAvailability(PaymentMethodType.Stripe, false),
                new PaymentMethodAvailability(PaymentMethodType.BankTransfer, null),
            });
    }

    protected sealed class FakeTax : ITaxCalculationService
    {
        public decimal Rate { get; set; }
        public List<TaxLineInput> LastLines { get; private set; } = new();

        public Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest request, CancellationToken cancellationToken = default)
        {
            LastLines = request.Lines;
            var taxable = request.Lines.Sum(l => l.Amount * l.Quantity - l.DiscountAmount);
            return Task.FromResult(new TaxBreakdown(Math.Round(taxable * Rate, 2), new List<TaxJurisdiction>(), false));
        }

        public Task ReportTransactionAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRouting : IOrderRoutingEngine
    {
        private readonly Guid _store;
        public FakeRouting(Guid store) => _store = store;
        public Task<RoutingResult> RouteAsync(RoutingRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RoutingResult(true, _store, null, Array.Empty<StoreEvaluation>()));
    }

    private sealed class FakeShipping : IShippingRateService
    {
        private readonly Func<decimal> _rate;
        public FakeShipping(Func<decimal> rate) => _rate = rate;
        public Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ShippingRateOption>>(new[] { new ShippingRateOption("std", "Standard", "Test", 3, _rate()) });
    }

    private sealed class FakeSelector : IShippingOptionSelector
    {
        public Task<ShippingSelectionResult> SelectAsync(IReadOnlyList<ShippingRateOption> options, CancellationToken ct) =>
            Task.FromResult(new ShippingSelectionResult(options, options.FirstOrDefault()));
    }

    private sealed class FakeCoupons : ICouponService
    {
        public Task<CouponValidationResult> ValidateAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(new CouponValidationResult(false, null, "invalid"));
        public Task RedeemAsync(string code, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTemplates : IEmailTemplateSender
    {
        public Task<bool> SendAsync(string templateKey, string recipientEmail, string? recipientName,
            IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    protected sealed class FakeCommerce : ICommerceModeService
    {
        public Task<CommerceModeSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(CommerceModeSettings.Default);
        public Task<bool> IsInquiryOnlyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}

internal static class QueryFilterExtensions
{
    public static IQueryable<T> IgnoreQueryFiltersSafe<T>(this IQueryable<T> query) where T : class =>
        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(query);
}
