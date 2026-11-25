using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Application.Tests.GiftCertificates;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Storefront;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Checkout;

/// <summary>
/// Guest checkout (docs/guest-checkout-requirements.md), end to end against the SQL Server test DB: the guest
/// record and email normalization (U-11, U-46, D6), the admin switch and its reasons (A-01/02/04, AC 4), guest
/// payment methods (A-03), the opt-in (U-14), order lookup and its rate limit (U-60/62), account creation from a
/// guest order (U-70/72) and order linking (U-71, A-18).
/// </summary>
public class GuestCheckoutTests : GiftCertificateTestBase
{
    private static string Session() => "s-" + Guid.NewGuid().ToString("N");

    private readonly IGuestOrderTokenService _tokens = new GuestOrderTokenService(new EphemeralDataProtectionProvider());

    private async Task Set(string key, string value)
    {
        await using var db = NewContext();
        await Settings(db).SetAsync(key, value);
    }

    private async Task<CheckoutResult> PlaceAs(string session, CheckoutAddress shipTo,
        PaymentMethodType method = PaymentMethodType.Stripe, bool optIn = false)
    {
        await using var db = NewContext();
        return await Orchestrator(db).PlaceAsync(new PlaceCheckoutRequest(
            null, session, shipTo, null, method, "tok_test", null, TermsAccepted: true, MarketingOptIn: optIn), default);
    }

    private async Task<CheckoutResult> PlaceNewCart(string email, bool optIn = false)
    {
        // Order numbers come from the clock: move it on so two orders in one test never share one.
        ((FixedClock)Clock).UtcNow = ((FixedClock)Clock).UtcNow.AddSeconds(1);
        var session = Session();
        await AddToCart(session, SeedSellable(20m));
        return await PlaceAs(session, ShipTo with { Email = email }, optIn: optIn);
    }

    // ---- guest record ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Guest_order_gets_a_guest_row_and_case_variants_group_as_one_guest()
    {
        var first = await PlaceNewCart("  Bob@X.com ");
        var second = await PlaceNewCart("bob@x.com");
        Assert.True(first.Success);
        Assert.True(second.Success);

        await using var db = NewContext();
        var guest = Assert.Single(db.Guests.ToList());
        Assert.Equal("bob@x.com", guest.EmailNormalized);
        var orders = db.Orders.Include(o => o.ShippingAddress).ToList();
        Assert.All(orders, o =>
        {
            Assert.Null(o.CustomerId);
            Assert.Equal(guest.Id, o.GuestId);
            Assert.Equal("bob@x.com", o.ShippingAddress!.Email);
        });

        var list = await new ListGuestCustomersQueryHandler(db).Handle(new ListGuestCustomersQuery(), default);
        var row = Assert.Single(list.Items);
        Assert.Equal(2, row.OrderCount);
        Assert.Equal(guest.Id, row.GuestId);
    }

    [Fact]
    public async Task Opt_in_marks_the_guest_and_subscribes_the_email()
    {
        Assert.True((await PlaceNewCart("news@example.com", optIn: true)).Success);

        await using var db = NewContext();
        Assert.True(db.Guests.Single().MarketingOptIn);
        var sub = db.CMSNewsletterSubscriptions.Single(s => s.Email == "news@example.com");
        Assert.Equal(NewsletterSubscriptionStatus.Subscribed, sub.Status);
        Assert.Equal("checkout", sub.Source);
    }

    [Fact]
    public void Place_validator_rejects_a_missing_or_malformed_email()
    {
        var validator = new PlaceCheckoutCommandValidator();
        PlaceCheckoutCommand Cmd(string email) => new(new PlaceCheckoutRequest(
            null, "s", ShipTo with { Email = email }, null, PaymentMethodType.Stripe, null, null));

        Assert.False(validator.Validate(Cmd("")).IsValid);
        Assert.False(validator.Validate(Cmd("not-an-email")).IsValid);
        Assert.False(validator.Validate(Cmd("a@b")).IsValid);
        Assert.False(validator.Validate(Cmd(new string('a', 250) + "@x.com")).IsValid);
        Assert.True(validator.Validate(Cmd(" Bob@Example.com ")).IsValid);
    }

    // ---- guest policy ----------------------------------------------------------------------------------------

    [Fact]
    public async Task AC4_switch_off_hides_guest_on_the_quote_and_refuses_a_direct_place()
    {
        await Set(GuestCheckoutSettingsStore.EnabledKey, "false");
        var session = Session();
        await AddToCart(session, SeedSellable(20m));

        var quote = await Quote(session);
        Assert.False(quote.GuestOrderingAllowed);
        Assert.Equal(GuestBlockReasons.Disabled, quote.GuestBlockedCode);
        Assert.False(string.IsNullOrWhiteSpace(quote.GuestBlockedReason));

        await Assert.ThrowsAsync<UnauthorizedException>(() => PlaceAs(session, ShipTo));
        Assert.Empty(Payments.Authorizations);
        await using var db = NewContext();
        Assert.False(db.Orders.Any());
    }

    [Fact]
    public async Task Guest_cannot_order_with_an_email_that_already_has_an_account()
    {
        await using (var db = NewContext())
        {
            db.Users.Add(new User { Username = "member", Email = "member@example.com", PasswordHash = "x", EmailVerified = true, IsActive = true, Customer = new Customer { FirstName = "M", LastName = "E" } });
            await db.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceNewCart("  Member@Example.com "));
        Assert.Contains("already exists", ex.Message);
        Assert.Empty(Payments.Authorizations);
        await using (var db = NewContext())
            Assert.False(db.Orders.Any());

        // A new email goes straight through.
        Assert.True((await PlaceNewCart("someone-new@example.com")).Success);
    }

    [Fact]
    public async Task Account_required_product_blocks_guest_checkout()
    {
        var product = SeedSellable(20m);
        await using (var db = NewContext())
        {
            await db.Products.Where(p => p.Id == product).ExecuteUpdateAsync(s => s.SetProperty(p => p.AccountRequired, true));
        }
        var session = Session();
        await AddToCart(session, product);

        Assert.Equal(GuestBlockReasons.AccountRequiredItem, (await Quote(session)).GuestBlockedCode);
        await Assert.ThrowsAsync<UnauthorizedException>(() => PlaceAs(session, ShipTo));

        await using var db2 = NewContext();
        var options = await new GetCheckoutOptionsQueryHandler(db2, new FakeCurrentUser(), Settings(db2))
            .Handle(new GetCheckoutOptionsQuery(null, session), default);
        Assert.False(options.GuestAllowed);
        Assert.Equal(GuestBlockReasons.AccountRequiredItem, options.GuestBlockedCode);
    }

    [Fact]
    public async Task Guest_order_over_the_limit_needs_an_account()
    {
        await Set(GuestCheckoutSettingsStore.MaxTotalKey, "50");
        var session = Session();
        await AddToCart(session, SeedSellable(80m));

        var quote = await Quote(session);
        Assert.Equal(GuestBlockReasons.OverLimit, quote.GuestBlockedCode);
        Assert.Contains("50.00", quote.GuestBlockedReason);
        await Assert.ThrowsAsync<UnauthorizedException>(() => PlaceAs(session, ShipTo));
    }

    [Fact]
    public async Task Guests_only_see_and_can_only_use_the_methods_the_admin_opened()
    {
        await Set(GuestCheckoutSettingsStore.PaymentMethodsKey, "[\"BankTransfer\"]");
        var session = Session();
        await AddToCart(session, SeedSellable(20m));

        var quote = await Quote(session);
        Assert.Equal(new[] { "BankTransfer" }, quote.AvailablePaymentMethods.Select(m => m.Method).ToArray());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceAs(session, ShipTo, PaymentMethodType.Stripe));
        Assert.Equal(CheckoutErrorCodes.PaymentMethodNotAllowed, ex.Extensions?["code"]);
    }

    // ---- lookup ----------------------------------------------------------------------------------------------

    private sealed class PassRecaptcha : IRecaptchaVerifier
    {
        public Task<RecaptchaVerificationResult> VerifyAsync(RecaptchaFormType formType, string? token, CancellationToken ct = default) =>
            Task.FromResult(new RecaptchaVerificationResult(true));
        public Task VerifyOrThrowAsync(RecaptchaFormType formType, string? token, CancellationToken ct = default) => Task.CompletedTask;
    }

    private async Task<GuestOrderLookupResult> Lookup(string number, string? email, string? zip = null)
    {
        await using var db = NewContext();
        return await new LookupGuestOrderCommandHandler(db, new FakeCurrentUser(), Clock, _tokens, Settings(db), new PassRecaptcha())
            .Handle(new LookupGuestOrderCommand(number, email, zip), default);
    }

    [Fact]
    public async Task Lookup_by_number_and_email_or_zip_opens_the_order_and_mismatches_are_rate_limited()
    {
        var placed = await PlaceNewCart("lookup@example.com");

        var byEmail = await Lookup(placed.OrderNumber, " LOOKUP@example.com ");
        await using (var db = NewContext())
        {
            var view = await new GetGuestOrderQueryHandler(db, _tokens).Handle(new GetGuestOrderQuery(byEmail.Token), default);
            Assert.Equal(placed.OrderNumber, view.Order.OrderNumber);
            Assert.False(view.AccountExists);
            Assert.Null(view.Order.PaymentTransactionId);
        }
        Assert.NotNull((await Lookup(placed.OrderNumber, null, "78701-1234")).Token);

        // Wrong email and wrong number give the same answer.
        var wrongEmail = await Assert.ThrowsAsync<NotFoundException>(() => Lookup(placed.OrderNumber, "someone@else.com"));
        var wrongNumber = await Assert.ThrowsAsync<NotFoundException>(() => Lookup("ORD-NOPE", "lookup@example.com"));
        Assert.Equal(wrongEmail.Message, wrongNumber.Message);

        for (var i = 0; i < LookupGuestOrderCommandHandler.MaxFailures - 2; i++)
            await Assert.ThrowsAsync<NotFoundException>(() => Lookup("ORD-NOPE", "x@y.com"));
        // The 6th try inside 15 minutes is refused even with the right details (acceptance criterion 8).
        await Assert.ThrowsAsync<TooManyRequestsException>(() => Lookup(placed.OrderNumber, "lookup@example.com"));
    }

    [Fact]
    public async Task Tampered_token_is_refused()
    {
        await using var db = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetGuestOrderQueryHandler(db, _tokens).Handle(new GetGuestOrderQuery("not-a-token"), default));
    }

    // ---- guest → account -------------------------------------------------------------------------------------

    [Fact]
    public async Task Account_from_guest_order_is_unverified_and_links_the_orders_once_verified()
    {
        var placed = await PlaceNewCart("convert@example.com");
        var token = _tokens.Generate(placed.OrderId, TimeSpan.FromDays(1));
        var emails = new FakeEmailTemplateSender();

        await using (var db = NewContext())
        {
            var result = await new CreateAccountFromGuestOrderCommandHandler(
                    db, _tokens, new FakePasswordHasher(), new FakeJwtTokenService(), Clock, emails, new FakeUrlBuilder())
                .Handle(new CreateAccountFromGuestOrderCommand(token, "secret123"), default);
            Assert.Equal("convert@example.com", result.Email);
        }
        Assert.Equal("account.email-verification", emails.LastTemplateKey);

        await using (var db = NewContext())
        {
            var user = db.Users.Include(u => u.Customer).Single(u => u.Email == "convert@example.com");
            Assert.False(user.EmailVerified);
            // N-07: nothing is linked before the email is verified.
            Assert.Null(db.Orders.Single(o => o.Id == placed.OrderId).CustomerId);
            Assert.Equal(2, db.CustomerAddresses.Count(a => a.CustomerId == user.Customer!.Id));

            // U-72: a second account for the same email is refused.
            await Assert.ThrowsAsync<ConflictException>(() => new CreateAccountFromGuestOrderCommandHandler(
                    db, _tokens, new FakePasswordHasher(), new FakeJwtTokenService(), Clock, emails, new FakeUrlBuilder())
                .Handle(new CreateAccountFromGuestOrderCommand(token, "secret123"), default));

            var verifyToken = db.UserTokens.Single(t => t.UserId == user.Id && t.Purpose == UserTokenPurpose.EmailVerification);
            var raw = verifyToken.TokenHash["tokenhash:".Length..];
            await new Features.CustomerAuth.VerifyEmailCommandHandler(db, new FakeJwtTokenService(), Clock, Settings(db))
                .Handle(new Features.CustomerAuth.VerifyEmailCommand(raw), default);
        }

        await using (var db = NewContext())
        {
            var user = db.Users.Include(u => u.Customer).Single(u => u.Email == "convert@example.com");
            Assert.True(user.EmailVerified);
            Assert.Equal(user.Customer!.Id, db.Orders.Single(o => o.Id == placed.OrderId).CustomerId);
            Assert.Equal(user.Customer.Id, db.Guests.Single().LinkedCustomerId);
        }
    }

    [Fact]
    public async Task Admin_link_moves_orders_to_the_matching_account_only()
    {
        var placed = await PlaceNewCart("linkme@example.com");
        Guid guestId;
        await using (var db = NewContext())
            guestId = db.Guests.Single().Id;

        await using (var db = NewContext())
            await Assert.ThrowsAsync<ConflictException>(() =>
                new LinkGuestOrdersCommandHandler(db, Clock).Handle(new LinkGuestOrdersCommand(guestId), default));

        Guid customerId;
        await using (var db = NewContext())
        {
            var user = new User { Username = "linkme", Email = "linkme@example.com", PasswordHash = "x", EmailVerified = true, IsActive = true, Customer = new Customer { FirstName = "L", LastName = "M" } };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            customerId = user.Customer.Id;
        }

        await using (var db = NewContext())
        {
            var result = await new LinkGuestOrdersCommandHandler(db, Clock).Handle(new LinkGuestOrdersCommand(guestId), default);
            Assert.Equal(1, result.LinkedOrders);
        }
        await using (var db = NewContext())
        {
            Assert.Equal(customerId, db.Orders.Single(o => o.Id == placed.OrderId).CustomerId);

            // A guest who registered stays on the Guest Customers screen, marked as registered.
            var row = Assert.Single((await new ListGuestCustomersQueryHandler(db).Handle(new ListGuestCustomersQuery(), default)).Items);
            Assert.True(row.IsRegistered);
            Assert.Equal(customerId, row.RegisteredCustomerId);
            Assert.Equal(1, row.OrderCount);
            Assert.Equal(0, row.UnlinkedOrderCount);
            Assert.NotNull(row.LinkedOnUtc);

            // The account filter splits registered guests from guest-only ones.
            Assert.Single((await new ListGuestCustomersQueryHandler(db).Handle(
                new ListGuestCustomersQuery(Account: GuestAccountFilter.Registered), default)).Items);
            Assert.Empty((await new ListGuestCustomersQueryHandler(db).Handle(
                new ListGuestCustomersQuery(Account: GuestAccountFilter.GuestOnly), default)).Items);

            // Their orders still show in the drill-down, flagged as moved to the account.
            var orders = await new ListGuestCustomerOrdersQueryHandler(db).Handle(new ListGuestCustomerOrdersQuery(guestId), default);
            Assert.True(Assert.Single(orders).LinkedToAccount);
        }
    }
}
