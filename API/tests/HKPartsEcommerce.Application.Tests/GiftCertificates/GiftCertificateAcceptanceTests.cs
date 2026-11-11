using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.GiftCertificates;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Features.Payments;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Email;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Security;
using Xunit;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Tests.GiftCertificates;

/// <summary>Gift Certificates acceptance list (1–21), end to end against the real SQL Server test DB.</summary>
public class GiftCertificateAcceptanceTests : GiftCertificateTestBase
{
    private static string Session() => "s-" + Guid.NewGuid().ToString("N");

    // ---- 1–3: admin create / adjust -----------------------------------------------------------------------

    [Fact]
    public async Task T01_admin_create_generates_code_and_ledger()
    {
        await using (var db = NewContext())
        {
            var settings = Settings(db);
            await settings.SetAsync(GiftCertificateRules.CodePrefixKey, "HK");
            await settings.SetAsync(GiftCertificateRules.CodeLengthKey, "12");
        }

        await using var ctx = NewContext();
        var created = await new CreateGiftCertificateHandler(ctx, Service(ctx), new FakeMailer(), new FakeCurrentUser(), Clock)
            .Handle(new CreateGiftCertificateCommand(null, 50m, GiftCertificateStatus.Active, null, null, null, null, false, "Goodwill"), default);

        Assert.StartsWith("HK", created.Code);
        Assert.Equal(14, created.Code.Length);
        Assert.All(created.Code[2..], c => Assert.Contains(c, GiftCertificateRules.Alphabet));
        Assert.Equal((50m, 50m), (created.OriginalBalance, created.CurrentBalance));
        var entry = Assert.Single(created.Ledger);
        Assert.Equal((GiftCertificateAction.Created, 50m, 50m, "Goodwill"), (entry.Action, entry.Amount, entry.BalanceAfter, entry.Comment));
    }

    private async Task<GiftCertificateDto> Update(Guid id, decimal? newBalance, string? comment, GiftCertificateStatus status = GiftCertificateStatus.Active)
    {
        string rowVersion;
        await using (var db = NewContext())
            rowVersion = Convert.ToBase64String(db.GiftCertificates.Single(g => g.Id == id).RowVersion);
        await using var ctx = NewContext();
        return await new UpdateGiftCertificateHandler(ctx, Service(ctx), new FakeCurrentUser(), Clock).Handle(
            new UpdateGiftCertificateCommand(null, id, status, null, "Robin", "robin@example.com", null, newBalance, null, comment, rowVersion), default);
    }

    [Fact]
    public async Task T02_balance_adjustment_writes_signed_ledger_and_keeps_original()
    {
        var gc = await SeedCertificate(50m);
        var updated = await Update(gc.Id, 30m, "Partial goodwill reversal");

        Assert.Equal((50m, 30m), (updated.OriginalBalance, updated.CurrentBalance));
        var adjust = updated.Ledger.Single(l => l.Action == GiftCertificateAction.AdminAdjusted);
        Assert.Equal((-20m, 30m), (adjust.Amount, adjust.BalanceAfter));
    }

    [Fact]
    public async Task T03_balance_change_needs_a_comment_and_cannot_go_negative()
    {
        var gc = await SeedCertificate(50m);
        var noComment = await Assert.ThrowsAsync<ValidationException>(() => Update(gc.Id, 30m, null));
        Assert.Contains("Comment", noComment.Errors.Keys);

        var validation = await new UpdateGiftCertificateValidator().ValidateAsync(
            new UpdateGiftCertificateCommand(null, gc.Id, GiftCertificateStatus.Active, null, null, null, null, -1m, null, "x", "AAAA"));
        Assert.Contains(validation.Errors, e => e.PropertyName == "NewBalance");

        var statusNoComment = await Assert.ThrowsAsync<ValidationException>(() => Update(gc.Id, null, " ", GiftCertificateStatus.Inactive));
        Assert.Contains("Comment", statusNoComment.Errors.Keys);
        Assert.Equal(50m, Balance(gc.Id));
    }

    // ---- 4–5: apply validity ------------------------------------------------------------------------------

    [Fact]
    public async Task T04_code_matching_is_trimmed_and_case_insensitive()
    {
        var gc = await SeedCertificate(50m);
        var session = Session();
        await AddToCart(session, SeedSellable(80m));

        var applied = await ApplyToCart(session, "  " + gc.Code.ToLowerInvariant() + "  ");
        Assert.Equal(gc.Code, applied.Code);
        Assert.Equal(50m, applied.ApplyAmount);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("zero")]
    [InlineData("expired")]
    [InlineData("unknown")]
    public async Task T05_unusable_codes_are_all_just_invalid(string kind)
    {
        var gc = await SeedCertificate(50m, g =>
        {
            if (kind == "inactive") g.Status = GiftCertificateStatus.Inactive;
            if (kind == "deleted") g.Deleted = true;
            if (kind == "zero") g.CurrentBalance = 0m;
            if (kind == "expired") g.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
        });
        var session = Session();
        await AddToCart(session, SeedSellable(80m));

        var ex = await Assert.ThrowsAsync<GiftCertificateUnavailableException>(() =>
            ApplyToCart(session, kind == "unknown" ? "NOPE2345678ABC" : gc.Code));
        Assert.Equal("Invalid Gift Certificate", ex.Message);
    }

    // ---- 6–11: checkout ------------------------------------------------------------------------------------

    [Fact]
    public async Task T06_partial_certificate_plus_card()
    {
        var gc = await SeedCertificate(50m);
        var session = Session();
        await AddToCart(session, SeedSellable(80m));
        await ApplyToCart(session, gc.Code);

        var quote = await Quote(session);
        Assert.Equal((80m, 50m, 30m), (quote.Total, quote.GiftCertificateApplied, quote.AmountDue));
        Assert.False(quote.PayWithGiftCertificate);

        var result = await Place(session);
        Assert.True(result.Success, result.Error);
        Assert.Equal(30m, Assert.Single(Payments.Authorizations).Amount);

        var payments = PaymentsOf(result.OrderId);
        Assert.Equal(2, payments.Count);
        var gcPayment = payments.Single(p => p.Method == PaymentMethodType.GiftCertificate);
        var card = payments.Single(p => p.Method == PaymentMethodType.Stripe);
        Assert.Equal((50m, false, gc.Code), (gcPayment.Amount, gcPayment.IsMaster, gcPayment.TransactionId));
        Assert.Equal((30m, true), (card.Amount, card.IsMaster));
        Assert.Equal(0m, Balance(gc.Id));
        var redeemed = Ledger(gc.Id).Single(l => l.Action == GiftCertificateAction.Redeemed);
        Assert.Equal((-50m, result.OrderId), (redeemed.Amount, redeemed.OrderId!.Value));
    }

    [Fact]
    public async Task T07_full_coverage_pays_with_the_certificate()
    {
        var gc = await SeedCertificate(50m);
        var session = Session();
        await AddToCart(session, SeedSellable(40m));
        await ApplyToCart(session, gc.Code);

        var quote = await Quote(session);
        Assert.True(quote.PayWithGiftCertificate);
        Assert.Equal(0m, quote.AmountDue);

        var result = await Place(session, PaymentMethodType.GiftCertificate, payWithGc: true);
        Assert.True(result.Success, result.Error);
        Assert.Empty(Payments.Authorizations);

        var order = LoadOrder(result.OrderId);
        Assert.Equal((OrderStatus.Processing, PaymentStatus.Captured, 40m), (order.Status, order.PaymentStatus, order.GiftCertificateAppliedAmount));
        Assert.True(Assert.Single(PaymentsOf(result.OrderId)).IsMaster);
        Assert.Equal(10m, Balance(gc.Id));
    }

    [Fact]
    public async Task T08_cart_summary_and_order_agree_on_the_applied_amount()
    {
        var gc = await SeedCertificate(50m);
        var session = Session();
        await AddToCart(session, SeedSellable(80m));
        await ApplyToCart(session, gc.Code);

        var cart = await GetCart(session);
        var quote = await Quote(session);
        var result = await Place(session);

        Assert.Equal(50m, cart.GiftCertificate!.ApplyAmount);
        Assert.Equal(50m, quote.GiftCertificateApplied);
        Assert.Equal(50m, LoadOrder(result.OrderId).GiftCertificateAppliedAmount);
        Assert.Equal(GiftCertificateRules.ApplyAmount(50m, 80m), cart.GiftCertificate.ApplyAmount);
    }

    [Fact]
    public async Task T09_deactivated_while_in_cart_is_removed_with_a_message()
    {
        var gc = await SeedCertificate(50m);
        var session = Session();
        await AddToCart(session, SeedSellable(80m));
        await ApplyToCart(session, gc.Code);

        await Update(gc.Id, null, "Fraud hold", GiftCertificateStatus.Inactive);

        var cart = await GetCart(session);
        Assert.Null(cart.GiftCertificate);
        Assert.Contains("The gift certificate you have submitted is no longer valid", cart.Warnings);

        // And at checkout (re-applying is refused, and the quote no longer carries it).
        var quote = await Quote(session);
        Assert.Null(quote.GiftCertificateCode);
        Assert.Equal(80m, quote.AmountDue);
    }

    [Fact]
    public async Task T09_checkout_step_detaches_a_certificate_that_became_invalid()
    {
        var gc = await SeedCertificate(50m);
        var session = Session();
        await AddToCart(session, SeedSellable(80m));
        await ApplyToCart(session, gc.Code);
        await using (var db = NewContext())
        {
            await db.GiftCertificates.Where(g => g.Id == gc.Id).ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, GiftCertificateStatus.Inactive));
        }

        var ex = await Assert.ThrowsAsync<GiftCertificateUnavailableException>(() => Quote(session));
        Assert.Equal(GiftCertificateRules.NoLongerValidMessage, ex.Message);
        Assert.Null((await GetCart(session)).GiftCertificate);
    }

    [Fact]
    public async Task T10_two_orders_cannot_both_spend_the_same_balance()
    {
        var gc = await SeedCertificate(50m);
        var a = Session();
        var b = Session();
        await AddToCart(a, SeedSellable(50m));
        await AddToCart(b, SeedSellable(50m));
        await ApplyToCart(a, gc.Code);
        await ApplyToCart(b, gc.Code);

        var first = await Place(a, PaymentMethodType.GiftCertificate, payWithGc: true);
        Assert.True(first.Success);
        var second = await Assert.ThrowsAsync<GiftCertificateUnavailableException>(() => Place(b, PaymentMethodType.GiftCertificate, payWithGc: true));
        Assert.Equal(GiftCertificateRules.NoLongerValidMessage, second.Message);
        Assert.Equal(0m, Balance(gc.Id));
        Assert.Single(Ledger(gc.Id), l => l.Action == GiftCertificateAction.Redeemed);
    }

    [Fact]
    public async Task T10_the_atomic_guard_rejects_a_stale_second_debit()
    {
        // Both orders already validated the certificate; the guarded UPDATE lets only one through.
        var gc = await SeedCertificate(50m);
        var orders = new List<Order>();
        await using (var db = NewContext())
        {
            for (var i = 0; i < 2; i++)
            {
                var order = new Order
                {
                    OrderNumber = $"RACE-{i}", Status = OrderStatus.Pending, PlacedOnUtc = Now, TotalAmount = 50m,
                    GiftCertificateId = gc.Id, GiftCertificateCode = gc.Code, GiftCertificateAppliedAmount = 50m,
                };
                db.Orders.Add(order);
                orders.Add(order);
            }
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
            await Service(db).RedeemForOrderAsync(db.Orders.Single(o => o.Id == orders[0].Id), coversFullAmount: true);
        await using (var db = NewContext())
            await Assert.ThrowsAsync<GiftCertificateUnavailableException>(() =>
                Service(db).RedeemForOrderAsync(db.Orders.Single(o => o.Id == orders[1].Id), coversFullAmount: true));

        Assert.Equal(0m, Balance(gc.Id));
        Assert.Empty(PaymentsOf(orders[1].Id));
    }

    [Fact]
    public async Task T11_failed_card_payment_leaves_the_balance_alone()
    {
        var gc = await SeedCertificate(50m);
        var session = Session();
        await AddToCart(session, SeedSellable(80m));
        await ApplyToCart(session, gc.Code);
        Payments.Fail = true;

        var result = await Place(session);

        Assert.False(result.Success);
        Assert.Equal(50m, Balance(gc.Id));
        Assert.DoesNotContain(Ledger(gc.Id), l => l.Action == GiftCertificateAction.Redeemed);
        Assert.DoesNotContain(PaymentsOf(result.OrderId), p => p.Method == PaymentMethodType.GiftCertificate);
    }

    // ---- 12–16: selling certificates -----------------------------------------------------------------------

    [Fact]
    public async Task T12_buying_two_online_certificates_issues_two_and_emails_each()
    {
        var product = SeedGiftCertificateProduct(25m);
        var session = Session();
        await AddToCart(session, product, qty: 2, gcDetails: true);

        var result = await Place(session);
        Assert.True(result.Success, result.Error);

        using var db = NewContext();
        var issued = db.GiftCertificates.Where(g => g.SourceOrderId == result.OrderId).ToList();
        Assert.Equal(2, issued.Count);
        Assert.All(issued, g =>
        {
            Assert.Equal((25m, 25m, GiftCertificateSource.Purchased, true), (g.OriginalBalance, g.CurrentBalance, g.Source, g.IsOnline));
            Assert.Equal(("Sam Sender", "rita@example.com"), (g.SenderName, g.RecipientEmail));
            Assert.NotNull(g.SourceOrderItemId);
        });
        Assert.Contains(Ledger(issued[0].Id), l => l.Action == GiftCertificateAction.Created && l.Comment!.StartsWith("Purchased with order #"));

        var emails = db.EmailQueue.Where(e => e.TemplateKey == GiftCertificateEmailTemplate.Key).ToList();
        Assert.Equal(2, emails.Count);
        Assert.All(emails, e =>
        {
            Assert.Equal(("rita@example.com", "sam@example.com", "sam@example.com"), (e.RecipientEmail, e.CcEmail, e.ReplyToEmail));
            Assert.Contains("Sam Sender sent you a", e.RenderedSubject);
            Assert.Contains(issued.Single(g => g.Id == e.RelatedEntityId).Code, e.RenderedBody);
        });
    }

    [Fact]
    public async Task T13_invoice_order_issues_only_once_paid()
    {
        var product = SeedGiftCertificateProduct(25m);
        var session = Session();
        await AddToCart(session, product, gcDetails: true);

        await using (var db = NewContext())
            await Settings(db).SetAsync(GiftCertificateRules.PurchasePaymentMethodsKey, "Stripe,BankTransfer");
        var result = await Place(session, PaymentMethodType.BankTransfer);
        Assert.True(result.Success, result.Error);
        using (var db = NewContext())
            Assert.False(db.GiftCertificates.Any(g => g.SourceOrderId == result.OrderId));

        // Marked paid (capture of the awaiting bank transfer).
        await using (var db = NewContext())
        {
            var paymentId = db.PaymentRecords.Single(p => p.OrderId == result.OrderId).Id;
            await new CapturePaymentCommandHandler(db, Payments, Clock, Service(db)).Handle(new CapturePaymentCommand(paymentId), default);
        }

        using (var db = NewContext())
            Assert.Single(db.GiftCertificates.Where(g => g.SourceOrderId == result.OrderId));
    }

    [Fact]
    public async Task T14_physical_certificate_is_issued_without_email_and_shown_on_the_order()
    {
        var product = SeedGiftCertificateProduct(30m, online: false);
        var session = Session();
        await AddToCart(session, product); // no recipient details needed for a physical card

        var result = await Place(session);

        await using var db = NewContext();
        var order = await new GetOrderQueryHandler(db).Handle(new GetOrderQuery(result.OrderId), default);
        var issued = Assert.Single(order.IssuedGiftCertificates);
        Assert.False(issued.IsOnline);
        Assert.Equal(30m, issued.Amount);
        Assert.False(db.EmailQueue.Any(e => e.TemplateKey == GiftCertificateEmailTemplate.Key));
    }

    [Fact]
    public async Task T15_certificate_purchase_limits_payment_methods_and_blocks_redeeming()
    {
        var gc = await SeedCertificate(50m);
        var session = Session();
        await AddToCart(session, SeedGiftCertificateProduct(25m), gcDetails: true);

        var quote = await Quote(session);
        Assert.Contains(quote.AvailablePaymentMethods, m => m.Method == "Stripe");
        Assert.DoesNotContain(quote.AvailablePaymentMethods, m => m.Method == "BankTransfer");

        var ex = await Assert.ThrowsAsync<GiftCertificateUnavailableException>(() => ApplyToCart(session, gc.Code));
        Assert.Equal("Gift certificates cannot be used to purchase gift certificates", ex.Message);

        // Allowed once the setting is on.
        await using (var db = NewContext())
            await Settings(db).SetAsync(GiftCertificateRules.AllowRedeemOnGcPurchaseKey, "true");
        Assert.Equal(25m, (await ApplyToCart(session, gc.Code)).ApplyAmount);
    }

    [Fact]
    public async Task T16_certificate_lines_are_untaxed_and_never_merge()
    {
        Tax.Rate = 0.10m;
        var product = SeedGiftCertificateProduct(25m);
        var session = Session();
        await AddToCart(session, product, gcDetails: true);
        var cart = await AddToCart(session, product, gcDetails: true);
        await AddToCart(session, SeedSellable(100m));

        Assert.Equal(2, cart.Items.Count(i => i.IsGiftCertificate));
        var quote = await Quote(session);
        Assert.Equal(10m, quote.TaxTotal); // 10% of the $100 part only
        Assert.DoesNotContain(Tax.LastLines, l => l.ProductId == product);
    }

    [Fact]
    public async Task Online_certificate_requires_sender_and_recipient()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => AddToCart(Session(), SeedGiftCertificateProduct(25m)));
        Assert.Contains("GcRecipientEmail", ex.Errors.Keys);
    }

    // ---- 17–19: refund, cancel, delete ---------------------------------------------------------------------

    [Fact]
    public async Task T17_refunding_the_certificate_payment_restores_the_balance()
    {
        var gc = await SeedCertificate(30m);
        var session = Session();
        await AddToCart(session, SeedSellable(30m));
        await ApplyToCart(session, gc.Code);
        var result = await Place(session, PaymentMethodType.GiftCertificate, payWithGc: true);
        Assert.Equal(0m, Balance(gc.Id));

        await using (var db = NewContext())
        {
            var paymentId = db.PaymentRecords.Single(p => p.OrderId == result.OrderId && p.Method == PaymentMethodType.GiftCertificate).Id;
            await new RefundPaymentCommandHandler(db, Payments, Clock, Service(db)).Handle(new RefundPaymentCommand(paymentId), default);
        }

        Assert.Equal(30m, Balance(gc.Id));
        var refund = Ledger(gc.Id).Single(l => l.Action == GiftCertificateAction.Refunded);
        Assert.Equal(30m, refund.Amount);
        Assert.Empty(Payments.Refunds); // never sent to a gateway
    }

    [Fact]
    public async Task T18_cancelling_an_order_voids_its_unused_purchased_certificate()
    {
        var session = Session();
        await AddToCart(session, SeedGiftCertificateProduct(25m), gcDetails: true);
        var result = await Place(session);

        await using (var db = NewContext())
        {
            await Service(db).HandleOrderCancelledAsync(result.OrderId);
        }

        using var check = NewContext();
        var gc = check.GiftCertificates.IgnoreQueryFilters().Single(g => g.SourceOrderId == result.OrderId);
        Assert.Equal((GiftCertificateStatus.Inactive, 0m), (gc.Status, gc.CurrentBalance));
        Assert.Contains(Ledger(gc.Id), l => l.Action == GiftCertificateAction.Voided && l.Amount == -25m);
    }

    [Fact]
    public async Task T18_partly_used_purchased_certificate_flags_the_order_instead()
    {
        var session = Session();
        await AddToCart(session, SeedGiftCertificateProduct(25m), gcDetails: true);
        var result = await Place(session);
        Guid gcId;
        await using (var db = NewContext())
        {
            gcId = db.GiftCertificates.Single(g => g.SourceOrderId == result.OrderId).Id;
            await Service(db).ApplyBalanceChangeAsync(gcId, -5m, GiftCertificateAction.Redeemed, null, "spent");
            await Service(db).HandleOrderCancelledAsync(result.OrderId);
        }

        Assert.Equal(20m, Balance(gcId));
        Assert.Contains("review manually", LoadOrder(result.OrderId).InternalNotes);
    }

    [Fact]
    public async Task T19_delete_soft_deletes_voids_and_blocks_redeeming()
    {
        var gc = await SeedCertificate(50m);
        await using (var db = NewContext())
            await new DeleteGiftCertificateHandler(db, Service(db), new FakeCurrentUser(), Clock).Handle(new DeleteGiftCertificateCommand(null, gc.Id), default);

        using (var db = NewContext())
        {
            var deleted = db.GiftCertificates.IgnoreQueryFilters().Single(g => g.Id == gc.Id);
            Assert.True(deleted.Deleted);
            Assert.Equal(GiftCertificateStatus.Inactive, deleted.Status);
        }

        Assert.Contains(Ledger(gc.Id), l => l.Action == GiftCertificateAction.Voided && l.Amount == -50m);
        var session = Session();
        await AddToCart(session, SeedSellable(10m));
        await Assert.ThrowsAsync<GiftCertificateUnavailableException>(() => ApplyToCart(session, gc.Code));
    }

    [Fact]
    public async Task Delete_is_blocked_while_an_open_unpaid_order_uses_it()
    {
        var gc = await SeedCertificate(50m);
        await using (var db = NewContext())
        {
            db.Orders.Add(new Order { OrderNumber = "OPEN-1", Status = OrderStatus.Pending, PaymentStatus = PaymentStatus.Pending, PlacedOnUtc = Now, GiftCertificateId = gc.Id });
            await db.SaveChangesAsync();
        }

        await using var ctx = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteGiftCertificateHandler(ctx, Service(ctx), new FakeCurrentUser(), Clock).Handle(new DeleteGiftCertificateCommand(null, gc.Id), default));
    }

    // ---- 20–21 ---------------------------------------------------------------------------------------------

    [Fact]
    public void T20_eleventh_apply_attempt_is_rate_limited()
    {
        var limiter = new GiftCertificateApplyRateLimiter(new MemoryCache(new MemoryCacheOptions()));
        for (var i = 0; i < 10; i++)
            Assert.True(limiter.TryConsume("1.2.3.4", "cart-a"));
        Assert.False(limiter.TryConsume("1.2.3.4", "cart-a"));     // 11th from this IP + cart
        Assert.False(limiter.TryConsume("1.2.3.4", "cart-b"));     // same IP, other cart: IP limit
        Assert.False(limiter.TryConsume("9.9.9.9", "cart-a"));     // other IP, same cart: cart limit
        Assert.True(limiter.TryConsume("9.9.9.9", "cart-c"));
    }

    [Fact]
    public async Task T21_purchased_by_is_the_purchasing_orders_buyer()
    {
        var session = Session();
        await AddToCart(session, SeedGiftCertificateProduct(25m), gcDetails: true);
        var purchase = await Place(session); // ShipTo = Alice Buyer
        ((FixedClock)Clock).UtcNow = Now.AddSeconds(1); // order numbers are timestamp-based
        string code;
        using (var db = NewContext())
            code = db.GiftCertificates.Single(g => g.SourceOrderId == purchase.OrderId).Code;

        // Redeemed later by a different shopper on another order.
        var other = Session();
        await AddToCart(other, SeedSellable(10m));
        await ApplyToCart(other, code);
        await using (var db = NewContext())
            await Orchestrator(db).PlaceAsync(new HKPartsEcommerce.Application.Common.Models.PlaceCheckoutRequest(
                null, other, ShipTo with { FirstName = "Bob", LastName = "Redeemer" }, null,
                PaymentMethodType.GiftCertificate, null, null, PayWithGiftCertificate: true, TermsAccepted: true), default);

        await using var ctx = NewContext();
        var list = await new ListGiftCertificatesHandler(ctx).Handle(new ListGiftCertificatesQuery(Search: code), default);
        var row = Assert.Single(list.Items);
        Assert.Equal("Alice Buyer", row.PurchasedBy);
        Assert.Equal((25m, 10m, 15m), (row.Amount, row.Spent, row.Balance));
    }

    // ---- invariants ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Balance_always_reconciles_with_the_ledger()
    {
        var gc = await SeedCertificate(50m);
        await Update(gc.Id, 35m, "adjust");
        await using (var db = NewContext())
        {
            await Service(db).ApplyBalanceChangeAsync(gc.Id, -10m, GiftCertificateAction.Redeemed, null, "use");
            await Service(db).ApplyBalanceChangeAsync(gc.Id, 4m, GiftCertificateAction.Refunded, null, "back");
        }

        var ledger = Ledger(gc.Id);
        Assert.Equal(ledger.Single(l => l.Action == GiftCertificateAction.Created).Amount
                     + ledger.Where(l => l.Action != GiftCertificateAction.Created).Sum(l => l.Amount), Balance(gc.Id));
        Assert.Equal(29m, Balance(gc.Id));
    }

    [Fact]
    public void Codes_are_masked_in_logs() =>
        Assert.Equal("****WXYZ", GiftCertificateRules.Mask("ABCDEFGHWXYZ"));

    private sealed class FakeMailer : IGiftCertificateMailer
    {
        public Task<bool> QueueAsync(Guid giftCertificateId, CancellationToken ct = default) => Task.FromResult(true);
    }
}
