using System.IO.Compression;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Analytics;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Features.Reports;
using HKPartsEcommerce.Application.Features.Rma;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Application.Tests.GiftCertificates;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Storefront;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Checkout;

/// <summary>
/// Guest checkout follow-ups (docs/guest-checkout-requirements.md) against the SQL Server test DB: cancel and return
/// requests from the guest order page (U-63), the guest share and customer-type filter on sales (A-41/A-42), Sales by
/// State, Abandoned Carts with the captured guest email (A-43), anonymisation (N-05) and the Excel export (A-17).
/// </summary>
public class GuestCheckoutFollowUpTests : GiftCertificateTestBase
{
    private static string Session() => "s-" + Guid.NewGuid().ToString("N");

    private readonly IGuestOrderTokenService _tokens = new GuestOrderTokenService(new EphemeralDataProtectionProvider());

    private async Task<CheckoutResult> PlaceGuest(string email, decimal price = 20m, CheckoutAddress? shipTo = null)
    {
        ((FixedClock)Clock).UtcNow = ((FixedClock)Clock).UtcNow.AddSeconds(1);
        var session = Session();
        await AddToCart(session, SeedSellable(price));
        await using var db = NewContext();
        return await Orchestrator(db).PlaceAsync(new PlaceCheckoutRequest(
            null, session, (shipTo ?? ShipTo) with { Email = email }, null, PaymentMethodType.Stripe, "tok_test", null, TermsAccepted: true), default);
    }

    private string TokenFor(Guid orderId) => _tokens.Generate(orderId, TimeSpan.FromDays(1));

    private async Task<GuestOrderViewDto> View(string token)
    {
        await using var db = NewContext();
        return await new GetGuestOrderQueryHandler(db, _tokens, Settings(db), Clock).Handle(new GetGuestOrderQuery(token), default);
    }

    // ---- U-63: cancel request ---------------------------------------------------------------------------------

    [Fact]
    public async Task Guest_can_ask_to_cancel_an_unshipped_order_once_and_staff_see_it()
    {
        var placed = await PlaceGuest("cancel@example.com");
        var token = TokenFor(placed.OrderId);
        Assert.True((await View(token)).CanRequestCancel);

        var emails = new FakeEmailEnqueuer();
        await using (var db = NewContext())
        {
            await db.Stores.Where(s => s.Id == StoreId).ExecuteUpdateAsync(s => s.SetProperty(x => x.NotificationEmail, "orders@store.test"));
        }
        await using (var db = NewContext())
        {
            var view = await new RequestGuestOrderCancelCommandHandler(db, _tokens, Clock, emails, new ViewSender(this))
                .Handle(new RequestGuestOrderCancelCommand(token, "Ordered by mistake", "Wrong size"), default);
            Assert.False(view.CanRequestCancel);
            Assert.Equal("Ordered by mistake: Wrong size", view.CancelRequestReason);
        }
        Assert.Equal("orders@store.test", emails.LastRecipient);

        await using (var db = NewContext())
        {
            var order = db.Orders.Single(o => o.Id == placed.OrderId);
            Assert.NotNull(order.CancelRequestedOnUtc);
            Assert.Contains(db.OrderStatusHistory.Where(h => h.OrderId == order.Id).ToList(), h => h.Note!.Contains("Cancellation requested"));
            var list = await new ListOrdersQueryHandler(db).Handle(new ListOrdersQuery(null), default);
            Assert.True(list.Items.Single(i => i.Id == order.Id).CancelRequested);

            // A second request is refused.
            await Assert.ThrowsAsync<ConflictException>(() => new RequestGuestOrderCancelCommandHandler(db, _tokens, Clock, emails, new ViewSender(this))
                .Handle(new RequestGuestOrderCancelCommand(token, "Other"), default));
        }
    }

    [Fact]
    public async Task Shipped_order_cannot_be_cancelled_by_the_guest()
    {
        var placed = await PlaceGuest("shipped@example.com");
        await using (var db = NewContext())
            await db.Orders.Where(o => o.Id == placed.OrderId).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Shipped).SetProperty(o => o.ShippedOnUtc, Now));

        Assert.False((await View(TokenFor(placed.OrderId))).CanRequestCancel);
        await using var db2 = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => new RequestGuestOrderCancelCommandHandler(db2, _tokens, Clock, new FakeEmailEnqueuer(), new ViewSender(this))
            .Handle(new RequestGuestOrderCancelCommand(TokenFor(placed.OrderId), "Other"), default));
    }

    /// <summary>The cancel handler returns the refreshed view through MediatR; this routes that one query.</summary>
    private sealed class ViewSender : MediatR.ISender
    {
        private readonly GuestCheckoutFollowUpTests _t;
        public ViewSender(GuestCheckoutFollowUpTests t) => _t = t;

        public async Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken ct = default)
            => (TResponse)(object)await _t.View(((GetGuestOrderQuery)(object)request).Token);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : MediatR.IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    // ---- U-63: return request ---------------------------------------------------------------------------------

    private sealed class NullFileStore : IPrivateFileStore
    {
        public Task<string> SaveAsync(string folder, string extension, Stream content, string contentType, CancellationToken ct = default)
            => Task.FromResult($"{folder}/{Guid.NewGuid():N}{extension}");
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Guest_can_request_a_return_on_a_delivered_order_with_the_order_link()
    {
        var placed = await PlaceGuest("return@example.com");
        Guid reasonId;
        await using (var db = NewContext())
        {
            await db.Orders.Where(o => o.Id == placed.OrderId).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Delivered)
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Captured)
                .SetProperty(o => o.ShippedOnUtc, Now.AddDays(-3))
                .SetProperty(o => o.DeliveredOnUtc, Now.AddDays(-1)));
            var reason = new RmaReason { Name = "Doesn't fit " + Guid.NewGuid().ToString("N")[..6] };
            db.RmaReasons.Add(reason);
            await db.SaveChangesAsync();
            reasonId = reason.Id;
            // Online returns are off until the admin switches them on (as in the returns tests).
            await ReturnSettingsStore.SaveAsync(Settings(db), new ReturnSettingsDto { Enabled = true, WindowDays = 30 }, default);
        }
        var token = TokenFor(placed.OrderId);
        Assert.True((await View(token)).CanRequestReturn);

        ReturnEligibilityDto eligibility;
        await using (var db = NewContext())
            eligibility = await new GetGuestOrderReturnabilityQueryHandler(db, _tokens, Settings(db), Clock)
                .Handle(new GetGuestOrderReturnabilityQuery(token), default);
        var line = eligibility.Lines.First(l => l.Eligible);

        await using (var db = NewContext())
        {
            var rma = await new SubmitRmaCommandHandler(db, new FakeCurrentUser(), new FakeEmailTemplateSender(), Settings(db), new NullFileStore(), Clock, _tokens)
                .Handle(new SubmitRmaCommand(Guid.Empty, new() { new RmaRequestLine(line.OrderLineItemId, 1, reasonId) },
                    AcceptPolicy: true, GuestToken: token), default);
            Assert.False(string.IsNullOrEmpty(rma.RmaNumber));
        }
        await using (var db = NewContext())
        {
            var saved = db.Rmas.Single(r => r.OrderId == placed.OrderId);
            Assert.Equal(RmaSource.Guest, saved.Source);
            Assert.Null(saved.CustomerId);
        }
        Assert.Single((await View(token)).Returns);
    }

    [Fact]
    public async Task Return_with_a_bad_token_is_refused()
    {
        await using var db = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new SubmitRmaCommandHandler(db, new FakeCurrentUser(), new FakeEmailTemplateSender(), Settings(db), new NullFileStore(), Clock, _tokens)
                .Handle(new SubmitRmaCommand(Guid.Empty, new() { new RmaRequestLine(Guid.NewGuid(), 1, Guid.NewGuid()) },
                    AcceptPolicy: true, GuestToken: "nope"), default));
    }

    // ---- A-41 / A-42: reports ---------------------------------------------------------------------------------

    private async Task CaptureAll()
    {
        await using var db = NewContext();
        await db.Orders.ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, PaymentStatus.Captured));
    }

    [Fact]
    public async Task Sales_summary_reports_the_guest_share_and_filters_by_customer_type()
    {
        await PlaceGuest("share1@example.com", 30m);
        await PlaceGuest("share2@example.com", 10m, ShipTo with { Region = "Texas" });
        await CaptureAll();

        await using var db = NewContext();
        var all = await new GetSalesSummaryQueryHandler(db, Clock).Handle(new GetSalesSummaryQuery("custom", Now.AddDays(-1), Now.AddDays(1)), default);
        Assert.Equal(2, all.TotalOrders);
        Assert.Equal(2, all.GuestOrders);
        Assert.Equal(100m, all.GuestOrderSharePercent);

        var registered = await new GetSalesSummaryQueryHandler(db, Clock).Handle(
            new GetSalesSummaryQuery("custom", Now.AddDays(-1), Now.AddDays(1), CustomerTypes.Registered), default);
        Assert.Equal(0, registered.TotalOrders);
        Assert.Equal(100m, registered.GuestOrderSharePercent); // the share is always against everything

        // Sales by State merges "TX" and "Texas" and splits guest from registered.
        var states = await new GetSalesByStateReportQueryHandler(db, Clock).Handle(
            new GetSalesByStateReportQuery("custom", Now.AddDays(-1), Now.AddDays(1)), default);
        var tx = Assert.Single(states);
        Assert.Equal("TX", tx.State);
        Assert.Equal(2, tx.Orders);
        Assert.Equal(2, tx.GuestOrders);
        Assert.Equal(0, tx.RegisteredOrders);
    }

    // ---- A-43: abandoned carts --------------------------------------------------------------------------------

    [Fact]
    public async Task Abandoned_cart_shows_the_email_the_guest_typed_and_their_opt_in()
    {
        var session = Session();
        await AddToCart(session, SeedSellable(20m));
        await using (var db = NewContext())
            await Orchestrator(db).QuoteAsync(new CheckoutQuoteRequest(null, session, ShipTo with { Email = " Left@Example.com " }, null,
                MarketingOptIn: true), default);

        // Cart and item timestamps come from the audit interceptor (real time), so "idle" is measured from now.
        ((FixedClock)Clock).UtcNow = DateTime.UtcNow.AddHours(3);
        await using (var db = NewContext())
        {
            var page = await new ListAbandonedCartsQueryHandler(db, Clock).Handle(
                new ListAbandonedCartsQuery(IdleHours: 1, CustomerType: CustomerTypes.Guest), default);
            var cart = Assert.Single(page.Items);
            Assert.True(cart.IsGuest);
            Assert.Equal("left@example.com", cart.Email);
            Assert.True(cart.MarketingOptIn);
            Assert.Equal(20m, cart.Subtotal);

            var csv = System.Text.Encoding.UTF8.GetString(await new ExportAbandonedCartsQueryHandler(db, Clock)
                .Handle(new ExportAbandonedCartsQuery(new ListAbandonedCartsQuery(IdleHours: 1)), default));
            Assert.Contains("left@example.com", csv);
        }
    }

    // ---- N-05: anonymise --------------------------------------------------------------------------------------

    [Fact]
    public async Task Anonymise_removes_personal_data_from_non_firearm_orders_and_keeps_firearm_ones()
    {
        var normal = await PlaceGuest("forget@example.com");
        var firearm = await PlaceGuest("forget@example.com");
        await using (var db = NewContext())
            await db.Orders.Where(o => o.Id == firearm.OrderId).ExecuteUpdateAsync(s => s.SetProperty(o => o.HasFflItems, true));

        Guid guestId;
        await using (var db = NewContext())
            guestId = db.Guests.Single().Id;

        await using (var db = NewContext())
        {
            var result = await new AnonymizeGuestCommandHandler(db, Clock).Handle(new AnonymizeGuestCommand(guestId), default);
            Assert.Equal(1, result.AnonymizedOrders);
            Assert.Equal(1, result.KeptFirearmOrders);
        }
        await using (var db = NewContext())
        {
            var anon = db.Orders.Include(o => o.ShippingAddress).Single(o => o.Id == normal.OrderId).ShippingAddress!;
            Assert.Equal("Deleted", anon.FirstName);
            Assert.Equal("Redacted", anon.AddressLine1);
            Assert.EndsWith("@anonymized.invalid", anon.Email);
            Assert.Null(anon.PhoneNumber);
            Assert.Equal("78701", anon.PostalCode); // geography kept for tax / reports

            var kept = db.Orders.Include(o => o.ShippingAddress).Single(o => o.Id == firearm.OrderId).ShippingAddress!;
            Assert.Equal("Alice", kept.FirstName);
            Assert.Equal("forget@example.com", kept.Email);

            var guest = db.Guests.Single();
            Assert.NotNull(guest.AnonymizedOnUtc);
            Assert.Equal("forget@example.com", guest.EmailNormalized); // firearm records stay traceable
        }
    }

    // ---- A-17: Excel export -----------------------------------------------------------------------------------

    [Fact]
    public async Task Guest_list_exports_as_a_valid_xlsx_workbook()
    {
        await PlaceGuest("excel@example.com");
        await using var db = NewContext();
        var bytes = await new ExportGuestCustomersQueryHandler(db).Handle(new ExportGuestCustomersQuery(Format: GuestExportFormat.Xlsx), default);

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!;
        using var reader = new StreamReader(sheet.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("excel@example.com", xml);
        Assert.Contains("Total spent", xml);
        _ = System.Xml.Linq.XDocument.Parse(xml); // well-formed
    }
}
