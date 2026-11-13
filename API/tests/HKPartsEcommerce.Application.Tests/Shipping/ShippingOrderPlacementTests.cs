using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Application.Tests.GiftCertificates;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Shipping;

/// <summary>
/// Shipping settings at order placement (docs/shipping-order-placement-requirements.md), end to end against
/// the SQL Server test DB: the frozen shipping row (OP-10..15), add-ons (SR-80..83), the price freeze
/// (OP-11/21/41), destination rules at placement (OP-54) and the double-submit guard (OP-56).
/// The test harness rates every cart with one option, "std" / "Standard" / carrier "Test", at ShippingRate.
/// </summary>
public class ShippingOrderPlacementTests : GiftCertificateTestBase
{
    private static string Session() => "s-" + Guid.NewGuid().ToString("N");

    private async Task<CheckoutQuote> QuoteWith(string session, ShippingAddOnSelection? addOns, CheckoutAddress? shipTo = null)
    {
        await using var db = NewContext();
        return await Orchestrator(db).QuoteAsync(new CheckoutQuoteRequest(null, session, shipTo ?? ShipTo, "std", AddOns: addOns), default);
    }

    private async Task<CheckoutResult> PlaceWith(
        string session, ShippingAddOnSelection? addOns = null, decimal? expectedShipping = null, decimal? expectedTotal = null,
        CheckoutAddress? shipTo = null)
    {
        await using var db = NewContext();
        return await Orchestrator(db).PlaceAsync(new PlaceCheckoutRequest(
            null, session, shipTo ?? ShipTo, "std", PaymentMethodType.Stripe, "tok_test", null,
            AddOns: addOns, ExpectedShippingTotal: expectedShipping, ExpectedTotal: expectedTotal, TermsAccepted: true), default);
    }

    private List<OrderShipping> ShippingsOf(Guid orderId)
    {
        using var db = NewContext();
        return db.OrderShippings.Where(s => s.OrderId == orderId).ToList();
    }

    private void SeedSpec(Guid productId, string attribute, string value)
    {
        using var db = NewContext();
        var attr = new SpecificationAttribute { Name = attribute };
        var option = new SpecificationAttributeOption { SpecificationAttribute = attr, Value = value };
        db.ProductSpecificationValues.Add(new ProductSpecificationValue { ProductId = productId, SpecificationAttributeOption = option });
        db.SaveChanges();
    }

    // ---- frozen shipping row ------------------------------------------------------------------------------

    [Fact]
    public async Task AC1_placed_order_carries_one_frozen_shipping_row()
    {
        ShippingRate = 16.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(40m), qty: 2);

        var result = await PlaceWith(session, expectedShipping: 16.50m, expectedTotal: 96.50m);

        Assert.True(result.Success);
        var row = Assert.Single(ShippingsOf(result.OrderId));
        Assert.Equal(OrderShipmentType.Combined, row.ShipmentType);
        Assert.Equal(ShipToType.Customer, row.ShipToType);
        Assert.Equal("std", row.MethodId);
        Assert.Equal("std", row.MethodName);
        Assert.Equal("Standard", row.DisplayName);
        Assert.Equal("Test", row.Carrier);
        Assert.Equal(16.50m, row.MethodPrice);
        Assert.Equal(16.50m, row.ShippingTotal);
        Assert.False(row.SignatureRequired);
        Assert.False(row.InsuranceSelected);
        Assert.Equal(80m, row.DeclaredValue);
        Assert.Equal(2, row.ItemCount);
        Assert.Equal(1, row.RulesVersion);
        Assert.Equal(16.50m, LoadOrder(result.OrderId).ShippingTotal);
    }

    [Fact]
    public async Task Order_dto_exposes_the_shipping_row()
    {
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(20m));
        var result = await PlaceWith(session, new ShippingAddOnSelection(RequireSignature: true));

        await using var db = NewContext();
        var dto = await new GetOrderQueryHandler(db).Handle(new GetOrderQuery(result.OrderId), default);
        var s = Assert.Single(dto.Shippings);
        Assert.Equal("Standard", s.DisplayName);
        Assert.True(s.SignatureRequired);
        Assert.Equal(5.00m, s.SignatureFee);
        Assert.Equal(14.50m, s.ShippingTotal);
    }

    // ---- add-ons --------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC4_both_add_ons_are_quoted_charged_and_frozen()
    {
        ShippingRate = 16.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(150m));
        var addOns = new ShippingAddOnSelection(RequireSignature: true, Insurance: true);

        var quote = await QuoteWith(session, addOns);
        Assert.Equal(16.50m, quote.ShippingMethodTotal);
        Assert.Equal(16.50m + 5.00m + 4.60m, quote.ShippingTotal);
        Assert.NotNull(quote.ShippingAddOns);
        Assert.Equal(5.00m, quote.ShippingAddOns!.SignatureFee);
        Assert.Equal(4.60m, quote.ShippingAddOns.InsuranceFee);
        Assert.Equal(150m, quote.ShippingAddOns.DeclaredValue);
        Assert.True(quote.ShippingAddOns.SignatureSelected && quote.ShippingAddOns.InsuranceSelected);

        var result = await PlaceWith(session, addOns, quote.ShippingTotal, quote.Total);

        Assert.True(result.Success);
        var row = Assert.Single(ShippingsOf(result.OrderId));
        Assert.Equal(16.50m, row.MethodPrice);
        Assert.True(row.SignatureRequired);
        Assert.Equal(5.00m, row.SignatureFee);
        Assert.True(row.InsuranceSelected);
        Assert.Equal(4.60m, row.InsuranceFee);
        Assert.Equal(26.10m, row.ShippingTotal);
        Assert.Equal(26.10m, LoadOrder(result.OrderId).ShippingTotal);
        Assert.Equal(176.10m, Payments.Authorizations.Single().Amount);
    }

    [Fact]
    public async Task Add_ons_not_chosen_cost_nothing_but_are_still_offered()
    {
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));

        var quote = await QuoteWith(session, addOns: null);

        Assert.Equal(9.50m, quote.ShippingTotal);
        Assert.True(quote.ShippingAddOns!.SignatureAvailable);
        Assert.True(quote.ShippingAddOns.InsuranceAvailable);
        Assert.Equal(2.75m, quote.ShippingAddOns.InsuranceFee);
        Assert.False(quote.ShippingAddOns.SignatureSelected);
    }

    [Fact]
    public async Task Disabled_add_ons_are_neither_offered_nor_charged()
    {
        await SaveRules(r => r with { SignatureEnabled = false, InsuranceEnabled = false });
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));

        var quote = await QuoteWith(session, new ShippingAddOnSelection(true, true));

        Assert.Equal(9.50m, quote.ShippingTotal);
        Assert.Null(quote.ShippingAddOns);
    }

    [Fact]
    public async Task Order_records_the_rules_version_that_priced_it()
    {
        var saved = await SaveRules(r => r with { SignatureFee = 7.25m });
        Assert.Equal(2, saved.Version);
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));

        var result = await PlaceWith(session, new ShippingAddOnSelection(RequireSignature: true));

        var row = Assert.Single(ShippingsOf(result.OrderId));
        Assert.Equal(7.25m, row.SignatureFee);
        Assert.Equal(2, row.RulesVersion);
    }

    // ---- price freeze ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Changed_shipping_price_is_refused_before_any_order_or_charge()
    {
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));
        var quote = await QuoteWith(session, null);

        ShippingRate = 12.00m; // the carrier rate moved (or an admin changed the method) after the method step

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceWith(session, null, quote.ShippingTotal, quote.Total));
        Assert.Contains("9.50", ex.Message);
        Assert.Contains("12.00", ex.Message);
        Assert.Empty(Payments.Authorizations);
        await using var db = NewContext();
        Assert.False(await db.Orders.AnyAsync());
    }

    [Fact]
    public async Task Fee_raised_by_an_admin_after_the_quote_is_refused()
    {
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));
        var addOns = new ShippingAddOnSelection(RequireSignature: true);
        var quote = await QuoteWith(session, addOns);

        await SaveRules(r => r with { SignatureFee = 8.00m });

        await Assert.ThrowsAsync<ConflictException>(() => PlaceWith(session, addOns, quote.ShippingTotal, quote.Total));
        Assert.Empty(Payments.Authorizations);
    }

    [Fact]
    public async Task Changed_cart_total_is_refused()
    {
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));
        var quote = await QuoteWith(session, null);
        await AddToCart(session, SeedSellable(5m, "Washer")); // another tab added an item

        await Assert.ThrowsAsync<ConflictException>(() => PlaceWith(session, null, quote.ShippingTotal, quote.Total));
        Assert.Empty(Payments.Authorizations);
    }

    [Fact]
    public async Task AC6_method_no_longer_offered_is_refused_before_charging()
    {
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));

        await using var db = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => Orchestrator(db).PlaceAsync(new PlaceCheckoutRequest(
            null, session, ShipTo, "deactivated-method", PaymentMethodType.Stripe, "tok", null, TermsAccepted: true), default));
        Assert.Empty(Payments.Authorizations);
    }

    // ---- destination rules at placement --------------------------------------------------------------------

    [Fact]
    public async Task AC7_state_restricted_product_to_a_restricted_state_is_blocked_at_quote_and_placement()
    {
        ShippingRate = 9.50m;
        var product = SeedSellable(30m, "Magazine");
        SeedSpec(product, ShippingRulesEngine.StateRestrictedAttribute, "Yes");
        var session = Session();
        await AddToCart(session, product);
        var california = ShipTo with { Region = "California", City = "Los Angeles", PostalCode = "90001" };

        var quoteEx = await Assert.ThrowsAsync<ConflictException>(() => QuoteWith(session, null, california));
        Assert.Contains("CA", quoteEx.Message);
        await Assert.ThrowsAsync<ConflictException>(() => PlaceWith(session, shipTo: california));
        Assert.Empty(Payments.Authorizations);

        // Texas is not restricted.
        Assert.True((await PlaceWith(session)).Success);
    }

    [Fact]
    public async Task Domestic_only_product_cannot_ship_abroad()
    {
        ShippingRate = 9.50m;
        var product = SeedSellable(30m);
        SeedSpec(product, ShippingRulesEngine.InternationalAttribute, "No");
        var session = Session();
        await AddToCart(session, product);
        var canada = ShipTo with { CountryCode = "CA", Region = "ON", City = "Toronto", PostalCode = "M5V 2T6" };

        await Assert.ThrowsAsync<ConflictException>(() => PlaceWith(session, shipTo: canada));
        Assert.Empty(Payments.Authorizations);
    }

    [Fact]
    public async Task Po_box_is_refused_when_the_rule_is_on()
    {
        await SaveRules(r => r with { RefusePoBoxes = true, PoBoxMessage = "No P.O. boxes, please." });
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceWith(session, shipTo: ShipTo with { Line1 = "PO Box 123" }));
        Assert.Equal("No P.O. boxes, please.", ex.Message);
    }

    // ---- double submit ---------------------------------------------------------------------------------------

    [Fact]
    public async Task AC9_second_submit_while_placing_is_refused_without_charging()
    {
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));
        await using (var db = NewContext())
        {
            var cart = await db.Carts.SingleAsync(c => c.SessionId == session);
            cart.PlacementLockUntilUtc = Now.AddMinutes(1); // the first request is still in flight
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<ConflictException>(() => PlaceWith(session));
        Assert.Empty(Payments.Authorizations);
    }

    [Fact]
    public async Task Lock_is_released_after_placement_returns()
    {
        ShippingRate = 9.50m;
        var session = Session();
        await AddToCart(session, SeedSellable(30m));
        Payments.Fail = true;

        var failed = await PlaceWith(session);
        Assert.False(failed.Success);

        await using var db = NewContext();
        Assert.Null((await db.Carts.SingleAsync(c => c.SessionId == session)).PlacementLockUntilUtc);
    }

    // ---- helpers ---------------------------------------------------------------------------------------------

    private async Task<ShippingRulesDto> SaveRules(Func<UpdateShippingRulesCommand, UpdateShippingRulesCommand> change)
    {
        var defaults = ShippingRulesDto.From(new ShippingRuleSettings());
        var command = change(new UpdateShippingRulesCommand(
            defaults.SignatureEnabled, defaults.SignatureFee, defaults.InsuranceEnabled, defaults.InsuranceBands,
            defaults.InsuranceExcessFeePer100, defaults.StateRestrictionEnabled, defaults.RestrictedStates,
            defaults.InternationalRestrictionEnabled, defaults.AllowedCountryCodes, defaults.RefusePoBoxes,
            defaults.PoBoxMessage, defaults.InvalidDestinationMessage));
        await using var db = NewContext();
        return await new UpdateShippingRulesCommandHandler(db).Handle(command, default);
    }
}

/// <summary>The shipping rules as pure functions (no database).</summary>
public class ShippingRulesEngineTests
{
    private static readonly IReadOnlyList<InsuranceBand> Bands = ShippingRulesEngine.ParseBands(null);

    [Theory]
    [InlineData(0, 2.75)]
    [InlineData(50, 2.75)]
    [InlineData(50.01, 3.50)]
    [InlineData(150, 4.60)]
    [InlineData(600, 12.25)]
    [InlineData(600.01, 14.15)]
    [InlineData(700, 14.15)]
    [InlineData(750, 16.05)]
    public void Insurance_fee_follows_the_legacy_bands(decimal declared, decimal fee) =>
        Assert.Equal(fee, ShippingRulesEngine.InsuranceFee(declared, Bands, 1.90m));

    private static DestinationCheckInput Input(string country = "US", string? region = "TX", string line1 = "1 Main St",
        bool intlBlocked = false, bool stateRestricted = false, bool exempt = false) =>
        new(country, region, line1, null, intlBlocked, stateRestricted, exempt);

    [Fact]
    public void Exempt_customer_may_receive_restricted_items_in_a_restricted_state() =>
        Assert.Null(ShippingRulesEngine.CheckDestination(new ShippingRuleSettings(),
            Input(region: "NY", stateRestricted: true, exempt: true)));

    [Fact]
    public void Restricted_state_matches_code_or_full_name()
    {
        var rules = new ShippingRuleSettings();
        Assert.NotNull(ShippingRulesEngine.CheckDestination(rules, Input(region: "ny", stateRestricted: true)));
        Assert.NotNull(ShippingRulesEngine.CheckDestination(rules, Input(region: "New Jersey", stateRestricted: true)));
        Assert.Null(ShippingRulesEngine.CheckDestination(rules, Input(region: "NY", stateRestricted: false)));
    }

    [Fact]
    public void Disallowed_country_is_refused_even_when_other_rules_pass()
    {
        var rules = new ShippingRuleSettings { AllowedCountryCodes = "US,CA" };
        Assert.NotNull(ShippingRulesEngine.CheckDestination(rules, Input(country: "MX", region: null)));
        Assert.Null(ShippingRulesEngine.CheckDestination(rules, Input(country: "CA", region: "ON")));
    }

    [Fact]
    public void International_block_only_applies_outside_the_us()
    {
        var rules = new ShippingRuleSettings();
        Assert.NotNull(ShippingRulesEngine.CheckDestination(rules, Input(country: "GB", region: null, intlBlocked: true)));
        Assert.Null(ShippingRulesEngine.CheckDestination(rules, Input(intlBlocked: true)));
    }

    [Theory]
    [InlineData("PO Box 12", true)]
    [InlineData("P.O. Box 12", true)]
    [InlineData("p o box 12", true)]
    [InlineData("Post Office Box 9", true)]
    [InlineData("100 Boxwood Lane", false)]
    [InlineData("1 Polo Drive", false)]
    public void Po_box_detection(string line, bool expected) =>
        Assert.Equal(expected, ShippingRulesEngine.IsPoBox(line));

    [Fact]
    public void Po_box_rule_is_off_by_default() =>
        Assert.Null(ShippingRulesEngine.CheckDestination(new ShippingRuleSettings(), Input(line1: "PO Box 5")));
}
