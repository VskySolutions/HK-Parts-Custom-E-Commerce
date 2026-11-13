using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Application.Tests.GiftCertificates;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Checkout;
using HKPartsEcommerce.Infrastructure.Persistence;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Checkout;

/// <summary>
/// Checkout settings at order placement (docs/checkout-shipping-payment-settings-requirements.md), end to end
/// against the SQL Server test DB: terms (SH-41), blacklist (PY-10/15/41), zero total (PY-40..42), session
/// expiry (CK-01/02) and the low-stock checkout lock (CK-20..23). AC numbers refer to section 9.
/// </summary>
public class CheckoutSettingsPlacementTests : GiftCertificateTestBase
{
    private static string Session() => "s-" + Guid.NewGuid().ToString("N");

    private FixedClock FixedClock => (FixedClock)Clock;

    private async Task<CheckoutQuote> QuoteAs(string session, bool withLocks = true)
    {
        await using var db = NewContext();
        return await Build(db, withLocks).QuoteAsync(new CheckoutQuoteRequest(null, session, ShipTo, "std"), default);
    }

    private async Task<CheckoutResult> PlaceAs(
        string session, bool? terms = true, PaymentMethodType method = PaymentMethodType.Stripe,
        CheckoutAddress? shipTo = null, bool withLocks = true)
    {
        await using var db = NewContext();
        return await Build(db, withLocks).PlaceAsync(new PlaceCheckoutRequest(
            null, session, shipTo ?? ShipTo, "std", method, "tok_test", null, TermsAccepted: terms), default);
    }

    /// <summary>The base composition plus the real SQL Server checkout lock.</summary>
    private CheckoutOrchestrator Build(AppDbContext db, bool withLocks) =>
        withLocks ? WithLocks(db) : Orchestrator(db);

    private async Task Set(string key, string value)
    {
        await using var db = NewContext();
        await Settings(db).SetAsync(key, value);
    }

    private static string? CodeOf(ConflictException ex) => ex.Extensions?["code"] as string;

    private void SetStock(Guid productId, int qty)
    {
        using var db = NewContext();
        db.InventoryLevels.Where(l => l.ProductId == productId)
            .ExecuteUpdate(s => s.SetProperty(l => l.StockQuantity, qty));
    }

    // ---- terms ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC6_place_without_terms_is_refused_before_any_charge()
    {
        var session = Session();
        await AddToCart(session, SeedSellable(20m));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceAs(session, terms: null));

        Assert.Equal(CheckoutErrorCodes.TermsRequired, CodeOf(ex));
        Assert.Empty(Payments.Authorizations);
    }

    [Fact]
    public async Task Terms_switched_off_lets_the_order_through_and_the_quote_says_so()
    {
        await Set(CheckoutSettingsStore.TermsRequiredKey, "false");
        var session = Session();
        await AddToCart(session, SeedSellable(20m));

        Assert.False((await QuoteAs(session)).TermsRequired);
        Assert.True((await PlaceAs(session, terms: null)).Success);
    }

    // ---- blacklist ------------------------------------------------------------------------------------------

    private void Blacklist(string? first, string? last, string? email, CustomerBlacklistStatus status = CustomerBlacklistStatus.Blocked)
    {
        using var db = NewContext();
        db.CustomerBlacklistEntries.Add(new CustomerBlacklistEntry
        {
            FirstName = first, LastName = last, Email = email, Reason = "Chargebacks", Status = status,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task AC9_blacklisted_email_is_blocked_before_the_gateway_and_logged()
    {
        Blacklist(null, null, "ALICE@example.com");
        var session = Session();
        await AddToCart(session, SeedSellable(20m));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceAs(session));

        Assert.Equal(CheckoutErrorCodes.CustomerBlocked, CodeOf(ex));
        Assert.Contains("blocked from placing orders", ex.Message);
        Assert.Empty(Payments.Authorizations);
        await using var db = NewContext();
        var hit = Assert.Single(db.CustomerBlacklistHits.ToList());
        Assert.Equal("Email", hit.MatchedOn);
        Assert.False(db.Orders.Any());
    }

    [Fact]
    public async Task Inactive_entry_and_name_only_match_in_email_only_mode_do_not_block()
    {
        Blacklist(null, null, "alice@example.com", CustomerBlacklistStatus.Inactive);
        Blacklist("Alice", "Buyer", null);
        await Set(CheckoutSettingsStore.BlacklistMatchModeKey, nameof(BlacklistMatchMode.EmailOnly));
        var session = Session();
        await AddToCart(session, SeedSellable(20m));

        Assert.True((await PlaceAs(session)).Success);
    }

    [Fact]
    public async Task AC12_blacklist_also_blocks_a_gift_certificate_order()
    {
        Blacklist("alice", "BUYER", null);
        var gc = await SeedCertificate(500m);
        var session = Session();
        await AddToCart(session, SeedSellable(20m));
        await ApplyToCart(session, gc.Code);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceAs(session, method: PaymentMethodType.GiftCertificate));

        Assert.Equal(CheckoutErrorCodes.CustomerBlocked, CodeOf(ex));
        Assert.Equal(500m, Balance(gc.Id));
    }

    // ---- zero total ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Zero_total_is_quoted_as_none_and_placed_without_a_gateway()
    {
        ShippingRate = 0m;
        var session = Session();
        await AddToCart(session, SeedSellable(0m));

        var quote = await QuoteAs(session);
        Assert.Equal(CheckoutPaymentModes.None, quote.PaymentMode);
        Assert.Empty(quote.AvailablePaymentMethods);

        var result = await PlaceAs(session, method: PaymentMethodType.NoPaymentRequired);

        Assert.True(result.Success);
        Assert.Empty(Payments.Authorizations);
        var order = LoadOrder(result.OrderId);
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal(PaymentStatus.Captured, order.PaymentStatus);
        var payment = Assert.Single(PaymentsOf(result.OrderId));
        Assert.Equal(PaymentMethodType.NoPaymentRequired, payment.Method);
        Assert.Equal(0m, payment.Amount);
        Assert.True(payment.IsMaster);
    }

    [Fact]
    public async Task No_payment_required_is_refused_once_the_total_is_not_zero()
    {
        ShippingRate = 5m;
        var session = Session();
        await AddToCart(session, SeedSellable(10m));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceAs(session, method: PaymentMethodType.NoPaymentRequired));

        Assert.Equal(CheckoutErrorCodes.TotalChanged, CodeOf(ex));
        Assert.Empty(Payments.Authorizations);
    }

    // ---- session expiry ------------------------------------------------------------------------------------

    [Fact]
    public async Task AC1_idle_checkout_expires()
    {
        var session = Session();
        await AddToCart(session, SeedSellable(20m));
        var quote = await QuoteAs(session);
        Assert.Equal(Now.AddSeconds(1800), quote.CheckoutExpiresUtc);

        FixedClock.UtcNow = Now.AddMinutes(31);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceAs(session));

        Assert.Equal(CheckoutErrorCodes.CheckoutExpired, CodeOf(ex));
        Assert.Empty(Payments.Authorizations);
    }

    // ---- low-stock checkout lock ---------------------------------------------------------------------------

    [Fact]
    public async Task AC2_AC3_low_stock_product_locks_to_the_first_cart_and_never_extends()
    {
        var product = SeedSellable(20m, "Last-unit barrel");
        SetStock(product, 3);
        var a = Session();
        var b = Session();
        await AddToCart(a, product);
        await AddToCart(b, product);

        var first = await QuoteAs(a);
        Assert.Equal(Now.AddSeconds(600), first.CheckoutLockExpiresUtc);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => QuoteAs(b));
        Assert.Equal(CheckoutErrorCodes.CheckoutLocked, CodeOf(ex));
        Assert.Contains("Last-unit barrel is currently being purchased", ex.Message);

        FixedClock.UtcNow = Now.AddMinutes(5);
        Assert.Equal(Now.AddSeconds(600), (await QuoteAs(a)).CheckoutLockExpiresUtc);

        // After the deadline cart B takes it over.
        FixedClock.UtcNow = Now.AddMinutes(11);
        Assert.Equal(Now.AddMinutes(11).AddSeconds(600), (await QuoteAs(b)).CheckoutLockExpiresUtc);
    }

    [Fact]
    public async Task CK23_placing_the_order_releases_the_lock()
    {
        var product = SeedSellable(20m);
        SetStock(product, 3);
        var a = Session();
        var b = Session();
        await AddToCart(a, product);
        await AddToCart(b, product);
        await QuoteAs(a);

        Assert.True((await PlaceAs(a)).Success);

        await using (var db = NewContext())
            Assert.False(db.CheckoutLocks.Any(l => l.ProductId == product));
        Assert.NotNull((await QuoteAs(b)).CheckoutLockExpiresUtc);
    }

    [Fact]
    public async Task AC16_threshold_setting_and_switch_control_locking()
    {
        await Set(CheckoutSettingsStore.LockQtyThresholdKey, "5");
        var product = SeedSellable(20m);
        SetStock(product, 7);
        var a = Session();
        await AddToCart(a, product);
        Assert.Null((await QuoteAs(a)).CheckoutLockExpiresUtc);

        SetStock(product, 4);
        await Set(CheckoutSettingsStore.LockEnabledKey, "false");
        Assert.Null((await QuoteAs(a)).CheckoutLockExpiresUtc);
    }

    // ---- state notice --------------------------------------------------------------------------------------

    [Fact]
    public async Task SH23_washington_ship_to_gets_the_state_notice()
    {
        var session = Session();
        await AddToCart(session, SeedSellable(20m));

        await using var db = NewContext();
        var quote = await WithLocks(db).QuoteAsync(
            new CheckoutQuoteRequest(null, session, ShipTo with { Region = "Washington" }, "std"), default);

        Assert.StartsWith("Due to recent changes to laws in Washington state", quote.StateNotice);
        Assert.Null((await QuoteAs(session)).StateNotice);
    }

    private CheckoutOrchestrator WithLocks(AppDbContext db) =>
        OrchestratorWith(db, new CheckoutLockService(db, Settings(db), Clock, NullLogger<CheckoutLockService>.Instance));
}
