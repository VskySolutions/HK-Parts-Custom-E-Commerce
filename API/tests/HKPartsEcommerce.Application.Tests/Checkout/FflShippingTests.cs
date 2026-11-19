using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Ffl;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Application.Tests.GiftCertificates;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Checkout;

/// <summary>
/// FFL shipping information (docs/ffl-shipping-information-requirements.md), end to end against the SQL Server
/// test DB: the dealer directory search, dealer selection on the cart, the FFL / non-FFL split at quote and
/// placement (one order, one shipping row per shipment), and the admin dealer change and import.
/// The test harness rates every shipment with one option, "std" at ShippingRate. Clock: 2026-11-04.
/// </summary>
public class FflShippingTests : GiftCertificateTestBase
{
    // Concord, NC 28025 (acceptance criteria §10).
    private const string Zip = "28025";
    private const double ZipLat = 35.4088, ZipLon = -80.5795;

    private static string Session() => "s-" + Guid.NewGuid().ToString("N");

    private Guid SeedFirearm(decimal price)
    {
        var id = SeedSellable(price, "Rifle");
        using var db = NewContext();
        db.Products.First(p => p.Id == id).IsFflRequired = true;
        db.SaveChanges();
        return id;
    }

    /// <summary>A dealer near Concord; "9C" expires March 2029 (valid), "5A" January 2025 (expired).</summary>
    private FflDealer SeedDealer(
        string seqn, string name, double lat, double lon, string type = "01", string expiry = "9C", bool? allowsTransfers = null)
    {
        using var db = NewContext();
        var d = new FflDealer
        {
            LicRegn = "1", LicDist = "56", LicCnty = "025", LicType = type, LicXprdte = expiry, LicSeqn = seqn,
            Key = "156" + seqn, LicenseName = name + " LLC", BusinessName = name,
            PremiseStreet = "3710 ZION CHURCH RD", PremiseCity = "CONCORD", PremiseState = "NC", PremiseZipCode = "280250000",
            VoicePhone = "7047860751", Latitude = lat, Longitude = lon, AllowsTransfers = allowsTransfers,
        };
        db.FflDealers.Add(d);
        db.SaveChanges();
        return d;
    }

    private void SeedZipCentre()
    {
        using var db = NewContext();
        if (!db.ZipGeocodes.Any(z => z.Zip == Zip))
        {
            db.ZipGeocodes.Add(new ZipGeocode { Zip = Zip, Latitude = ZipLat, Longitude = ZipLon, Source = "Test" });
            db.SaveChanges();
        }
    }

    private async Task<CheckoutFflStateDto> SelectDealer(string session, string key)
    {
        await using var db = NewContext();
        return await new SelectCheckoutFflDealerCommandHandler(db, new FakeCurrentUser(), Settings(db), Clock)
            .Handle(new SelectCheckoutFflDealerCommand(null, session, key), default);
    }

    private async Task<CheckoutQuote> QuoteFor(string session, CheckoutAddress? shipTo = null)
    {
        await using var db = NewContext();
        return await Orchestrator(db).QuoteAsync(new CheckoutQuoteRequest(null, session, shipTo ?? ShipTo, "std", FflShippingMethodId: "std"), default);
    }

    // The FFL-only page always sends the buyer's billing address (the ship-to becomes the dealer).
    private static readonly CheckoutAddress BillTo =
        new("Alice", "Buyer", "alice@example.com", "9 Billing Rd", null, "Concord", "NC", "28025", "US");

    private async Task<CheckoutResult> PlaceFor(string session, decimal? expectedShipping = null, CheckoutAddress? billTo = null, bool sendBillTo = true)
    {
        await using var db = NewContext();
        return await Orchestrator(db).PlaceAsync(new PlaceCheckoutRequest(
            null, session, ShipTo, "std", PaymentMethodType.Stripe, "tok_test", null,
            ExpectedShippingTotal: expectedShipping, TermsAccepted: true, SelectedFflShippingMethodId: "std",
            BillTo: sendBillTo ? billTo ?? BillTo : null), default);
    }

    [Fact]
    public async Task Ffl_only_order_stores_the_billing_address_and_requires_it()
    {
        ShippingRate = 40m;
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));
        var dealer = SeedDealer("10090", "CARTERS GUNSMITHING", 35.41, -80.58);
        await SelectDealer(session, dealer.Key);
        await QuoteFor(session);

        // No billing address: the ship-to is the dealer, so there is nothing to fall back on.
        await Assert.ThrowsAsync<HKPartsEcommerce.Application.Common.Exceptions.ValidationException>(
            () => PlaceFor(session, expectedShipping: 40m, sendBillTo: false));
        // FFL items bill to the United States only (SH-02).
        await Assert.ThrowsAsync<HKPartsEcommerce.Application.Common.Exceptions.ConflictException>(
            () => PlaceFor(session, expectedShipping: 40m, billTo: BillTo with { CountryCode = "CA", Region = "ON", PostalCode = "M5V 2T6" }));

        var result = await PlaceFor(session, expectedShipping: 40m);
        Assert.True(result.Success);
        await using var db = NewContext();
        var bill = db.Orders.Include(o => o.BillingAddress).Single(o => o.Id == result.OrderId).BillingAddress!;
        Assert.Equal("9 Billing Rd", bill.AddressLine1);
        Assert.Equal("28025", bill.PostalCode);
    }

    [Fact]
    public async Task Delivery_order_without_a_billing_address_bills_to_the_delivery_address()
    {
        ShippingRate = 10m;
        var session = Session();
        await AddToCart(session, SeedSellable(20m, "Sling"));
        await QuoteFor(session);

        var result = await PlaceFor(session, expectedShipping: 10m, sendBillTo: false);
        Assert.True(result.Success);
        await using var db = NewContext();
        var bill = db.Orders.Include(o => o.BillingAddress).Single(o => o.Id == result.OrderId).BillingAddress!;
        Assert.Equal(ShipTo.Line1, bill.AddressLine1);
        Assert.Equal(ShipTo.PostalCode, bill.PostalCode);
    }

    private async Task<FflSearchResultDto> Search(string? zip, int? radius, FflSearchContext context = FflSearchContext.Checkout)
    {
        await using var db = NewContext();
        return await new SearchFflDealersQueryHandler(db, new NoGeocoder(), Settings(db), Clock, new FakeCurrentUser(),
            NullLogger<SearchFflDealersQueryHandler>.Instance).Handle(new SearchFflDealersQuery(zip, radius, context), default);
    }

    // ---- directory rules ---------------------------------------------------------------------------------

    [Fact]
    public void Licence_number_key_expiry_and_phone_follow_the_legacy_rules()
    {
        var d = new FflDealer { LicRegn = "1", LicDist = "56", LicCnty = "25", LicType = "1", LicXprdte = "9C", LicSeqn = "01234" };
        Assert.Equal("1-56-025-01-9C-01234", FflDirectory.BuildFflNumber(d));
        Assert.Equal("15601234", FflDirectory.KeyFromFflNumber("1-56-025-01-9C-01234"));
        Assert.Null(FflDirectory.KeyFromFflNumber("manual entry 12345")); // FFL-26: never throws
        Assert.Equal(new DateOnly(2029, 3, 31), FflDirectory.LicenseExpiry("9C", Now));
        Assert.Equal(new DateOnly(2025, 1, 31), FflDirectory.LicenseExpiry("5A", Now));
        // ATF month letters skip I: J = September, M = December.
        Assert.Equal(new DateOnly(2027, 9, 30), FflDirectory.LicenseExpiry("7J", Now));
        Assert.Equal(new DateOnly(2026, 12, 31), FflDirectory.LicenseExpiry("6M", Now));
        Assert.Null(FflDirectory.LicenseExpiry("6I", Now));
        Assert.Equal("(704) 786-0751", FflDirectory.FormatPhone("7047860751"));
        Assert.Equal("786-0751", FflDirectory.FormatPhone("786-0751")); // FFL-46: raw, never "null"
        Assert.Equal("28025", FflDirectory.NormalizeZip("28025-1234"));
        Assert.Null(FflDirectory.NormalizeZip("abcde"));
    }

    // ---- search ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC2_radius_search_returns_eligible_dealers_nearest_first()
    {
        SeedZipCentre();
        SeedDealer("10001", "FAR SIDE ARMS", 35.50, -80.60);         // ~6 mi
        SeedDealer("10002", "CARTERS GUNSMITHING", 35.41, -80.58);   // ~0.1 mi
        SeedDealer("10003", "COLLECTOR ONLY", 35.41, -80.58, type: "03");
        SeedDealer("10004", "NO TRANSFERS", 35.41, -80.58, allowsTransfers: false);
        SeedDealer("10005", "EXPIRED ARMS", 35.41, -80.58, expiry: "5A");
        SeedDealer("10006", "OUT OF RANGE", 36.50, -80.60);          // ~75 mi

        var result = await Search(Zip, 15);

        Assert.Equal(new[] { "CARTERS GUNSMITHING", "FAR SIDE ARMS" }, result.Dealers.Select(d => d.Name).ToArray());
        Assert.Equal(2, result.TotalFound);
        Assert.Null(result.Message);
        var nearest = result.Dealers[0];
        Assert.Equal("(704) 786-0751", nearest.PhoneDisplay);
        Assert.StartsWith("https://www.google.com/maps/search/?api=1&query=", nearest.MapUrl);
        Assert.True(nearest.DistanceMiles < 1);

        var wider = await Search(Zip, 100);
        Assert.Equal(3, wider.Dealers.Count);
    }

    [Fact]
    public async Task Search_refuses_a_bad_zip_or_an_unlisted_radius_and_explains_no_results()
    {
        SeedZipCentre();
        var badZip = await Assert.ThrowsAsync<ValidationException>(() => Search("abcde", 15));
        Assert.Contains(FflMessages.InvalidZip, badZip.Errors.SelectMany(e => e.Value));
        await Assert.ThrowsAsync<ValidationException>(() => Search(Zip, 5000)); // FFL-36

        var none = await Search(Zip, 15);
        Assert.Empty(none.Dealers);
        Assert.Equal(FflMessages.NoResults(15, Zip), none.Message);
    }

    [Fact]
    public async Task Search_without_a_geocoder_uses_the_directory_centre_of_the_zip()
    {
        SeedDealer("10010", "CARTERS GUNSMITHING", 35.41, -80.58);
        var result = await Search(Zip, 15, FflSearchContext.Public);
        Assert.Single(result.Dealers);

        await using var db = NewContext();
        Assert.Equal("Directory", db.ZipGeocodes.Single(z => z.Zip == Zip).Source);
    }

    // ---- selection ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Selecting_an_ineligible_or_unknown_dealer_is_refused()
    {
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));
        var expired = SeedDealer("10020", "EXPIRED ARMS", 35.41, -80.58, expiry: "5A");
        var refusing = SeedDealer("10021", "NO TRANSFERS", 35.41, -80.58, allowsTransfers: false);

        await Assert.ThrowsAsync<ConflictException>(() => SelectDealer(session, expired.Key));
        await Assert.ThrowsAsync<ConflictException>(() => SelectDealer(session, refusing.Key));
        await Assert.ThrowsAsync<NotFoundException>(() => SelectDealer(session, "99999999"));
    }

    // ---- checkout ----------------------------------------------------------------------------------------

    [Fact]
    public async Task AC4_ffl_cart_without_a_dealer_quotes_but_cannot_be_placed()
    {
        ShippingRate = 40m;
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));

        var quote = await QuoteFor(session);
        Assert.NotNull(quote.Ffl);
        Assert.True(quote.Ffl!.DealerRequired);
        Assert.Empty(quote.Ffl.ShippingOptions);
        Assert.Empty(quote.ShippingOptions);
        Assert.False(quote.Ffl.HasNonFflItems);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => PlaceFor(session));
        Assert.Equal(FflMessages.DealerRequired, ex.Message);
    }

    [Fact]
    public async Task AC11_ffl_items_need_a_us_address()
    {
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));
        var canada = ShipTo with { CountryCode = "CA", Region = "ON", PostalCode = "M5V 2T6", City = "Toronto" };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => QuoteFor(session, canada));
        Assert.Equal(FflMessages.UsOnly, ex.Message);
    }

    [Fact]
    public async Task AC6_ffl_only_order_ships_to_the_dealer_read_from_the_directory()
    {
        ShippingRate = 40m;
        var session = Session();
        var rifle = SeedFirearm(500m);
        await AddToCart(session, rifle);
        var dealer = SeedDealer("10030", "CARTERS GUNSMITHING", 35.41, -80.58);
        await SelectDealer(session, dealer.Key);

        var quote = await QuoteFor(session);
        Assert.NotNull(quote.Ffl!.Dealer);
        Assert.Equal("std", quote.Ffl.SelectedMethodId);
        Assert.Equal(40m, quote.ShippingTotal);

        var result = await PlaceFor(session, expectedShipping: 40m);
        Assert.True(result.Success);

        var order = LoadOrder(result.OrderId);
        Assert.True(order.HasFflItems);
        Assert.Equal("Standard", order.ShippingMethodName);
        await using var db = NewContext();
        var row = Assert.Single(db.OrderShippings.Where(s => s.OrderId == result.OrderId).ToList());
        Assert.Equal(OrderShipmentType.Ffl, row.ShipmentType);
        Assert.Equal(ShipToType.FflDealer, row.ShipToType);
        Assert.Equal(FflDealerSource.Directory, row.FflSource);
        Assert.Equal(dealer.Key, row.FflDealerKey);
        Assert.Equal("CARTERS GUNSMITHING", row.FflDealerName);
        Assert.Equal("1-56-025-01-9C-10030", row.FflNumber);
        Assert.Equal("3710 ZION CHURCH RD", row.FflDealerStreet);
        Assert.True(row.ContainsFflItems);
        Assert.True(db.OrderLineItems.Single(l => l.OrderId == result.OrderId).IsFfl);
        // The buyer stays the order contact (emails greet them, not the dealer).
        var address = db.Orders.Include(o => o.ShippingAddress).Single(o => o.Id == result.OrderId).ShippingAddress!;
        Assert.Equal("Alice", address.FirstName);
    }

    [Fact]
    public async Task AC13_mixed_cart_is_one_order_with_a_shipment_per_destination()
    {
        ShippingRate = 10m;
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));
        await AddToCart(session, SeedSellable(20m, "Sling"));
        var dealer = SeedDealer("10040", "CARTERS GUNSMITHING", 35.41, -80.58);
        await SelectDealer(session, dealer.Key);

        var quote = await QuoteFor(session);
        Assert.True(quote.Ffl!.HasNonFflItems);
        Assert.Single(quote.ShippingOptions);
        Assert.Equal(10m, quote.Ffl.ShippingTotal);
        Assert.Equal(500m, quote.Ffl.DeclaredValue); // FFL-76: the FFL items' value, not the cart's
        Assert.Equal(20m, quote.ShippingTotal);

        var result = await PlaceFor(session, expectedShipping: 20m);
        Assert.True(result.Success);

        await using var db = NewContext();
        var rows = db.OrderShippings.Where(s => s.OrderId == result.OrderId).OrderBy(s => s.ShipmentType).ToList();
        Assert.Equal(new[] { OrderShipmentType.NonFfl, OrderShipmentType.Ffl }, rows.Select(r => r.ShipmentType).ToArray());
        Assert.Equal(ShipToType.Customer, rows[0].ShipToType);
        Assert.Equal(20m, rows[0].DeclaredValue);
        Assert.Equal(ShipToType.FflDealer, rows[1].ShipToType);
        Assert.Equal(500m, rows[1].DeclaredValue);
        var order = LoadOrder(result.OrderId);
        Assert.Equal("Mixed", order.ShippingMethodName);
        Assert.Equal(20m, order.ShippingTotal);
        Assert.Equal(1, db.OrderLineItems.Count(l => l.OrderId == result.OrderId && l.IsFfl));
    }

    [Fact]
    public async Task FFL59_dealer_that_stops_accepting_transfers_blocks_placement()
    {
        ShippingRate = 40m;
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));
        var dealer = SeedDealer("10050", "CARTERS GUNSMITHING", 35.41, -80.58);
        await SelectDealer(session, dealer.Key);

        await using (var db = NewContext())
        {
            db.FflDealers.Single(d => d.Key == dealer.Key).AllowsTransfers = false;
            await db.SaveChangesAsync();
        }

        var quote = await QuoteFor(session);
        Assert.True(quote.Ffl!.DealerRequired);
        Assert.Equal(FflMessages.DealerNotEligible, quote.Ffl.DealerMessage);
        await Assert.ThrowsAsync<ConflictException>(() => PlaceFor(session));
    }

    [Fact]
    public async Task Firearms_disabled_checks_out_like_any_product()
    {
        ShippingRate = 40m;
        await using (var db = NewContext())
            await Settings(db).SetAsync(FflSettingsStore.EnabledKey, "false");
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));

        var quote = await QuoteFor(session);
        Assert.Null(quote.Ffl);
        var result = await PlaceFor(session, expectedShipping: 40m);
        Assert.True(result.Success);
        Assert.False(LoadOrder(result.OrderId).HasFflItems);
    }

    // ---- admin -------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC14_admin_changes_the_dealer_until_the_order_ships()
    {
        ShippingRate = 40m;
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));
        var first = SeedDealer("10060", "CARTERS GUNSMITHING", 35.41, -80.58);
        var second = SeedDealer("10061", "FAR SIDE ARMS", 35.50, -80.60);
        await SelectDealer(session, first.Key);
        var placed = await PlaceFor(session, expectedShipping: 40m);

        await using (var db = NewContext())
        {
            var result = await new ChangeOrderFflDealerCommandHandler(db, new FakeCurrentUser { Email = "admin@example.com" }, Clock,
                NullLogger<ChangeOrderFflDealerCommandHandler>.Instance).Handle(new ChangeOrderFflDealerCommand(placed.OrderId, second.Key), default);
            Assert.Equal(FflMessages.AdminDealerChanged, result.Message);
        }

        await using (var db = NewContext())
        {
            var row = db.OrderShippings.Single(s => s.OrderId == placed.OrderId);
            Assert.Equal(second.Key, row.FflDealerKey);
            Assert.Equal(FflDealerSource.Admin, row.FflSource);
            Assert.Contains(db.OrderStatusHistory.Where(h => h.OrderId == placed.OrderId).Select(h => h.Note).ToList(),
                n => n != null && n.Contains("CARTERS GUNSMITHING") && n.Contains("FAR SIDE ARMS") && n.Contains("admin@example.com"));

            db.Orders.Single(o => o.Id == placed.OrderId).Status = OrderStatus.Shipped;
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            await Assert.ThrowsAsync<ConflictException>(() => new ChangeOrderFflDealerCommandHandler(db, new FakeCurrentUser(), Clock,
                NullLogger<ChangeOrderFflDealerCommandHandler>.Instance).Handle(new ChangeOrderFflDealerCommand(placed.OrderId, first.Key), default));
        }
    }

    [Fact]
    public async Task AC15_dealer_data_no_transfers_removes_the_dealer_from_searches()
    {
        SeedZipCentre();
        var dealer = SeedDealer("10070", "CARTERS GUNSMITHING", 35.41, -80.58);
        Assert.Single((await Search(Zip, 15)).Dealers);

        await using (var db = NewContext())
            await new UpdateFflDealerDataCommandHandler(db, Clock)
                .Handle(new UpdateFflDealerDataCommand(dealer.Key, AllowsTransfers: false, LastTransferFee: 25m), default);

        Assert.Empty((await Search(Zip, 15)).Dealers);
        await using var check = NewContext();
        Assert.Equal(25m, check.FflDealers.Single(d => d.Key == dealer.Key).LastTransferFee);
    }

    [Fact]
    public async Task Import_upserts_the_atf_list_and_keeps_the_store_dealer_data()
    {
        var existing = SeedDealer("20001", "OLD NAME", 35.41, -80.58, allowsTransfers: false);
        const string header = "LIC_REGN\tLIC_DIST\tLIC_CNTY\tLIC_TYPE\tLIC_XPRDTE\tLIC_SEQN\tLICENSE_NAME\tBUSINESS_NAME\tPREMISE_STREET\tPREMISE_CITY\tPREMISE_STATE\tPREMISE_ZIP_CODE\tMAIL_STREET\tMAIL_CITY\tMAIL_STATE\tMAIL_ZIP_CODE\tVOICE_PHONE";
        var file = string.Join("\n",
            header,
            "1\t56\t025\t01\t9C\t20001\tNEW LLC\tNEW NAME\t3710 ZION CHURCH RD\tCONCORD\tNC\t280250000\t\t\t\t\t7047860751",
            "1\t56\t025\t07\t8F\t20002\tMAKER LLC\t\t1 MAIN ST\tCONCORD\tNC\t280270000\t\t\t\t\t7045550100",
            "x\t56\t025\t01\t9C\t20003\tBAD ROW\t\t\t\t\t\t\t\t\t\t");

        ImportFflDealersResult result;
        await using (var db = NewContext())
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
            result = await new ImportFflDealersCommandHandler(db, Clock, NullLogger<ImportFflDealersCommandHandler>.Instance)
                .Handle(new ImportFflDealersCommand(stream, DeactivateMissing: false), default);
        }

        Assert.Equal(3, result.RowsRead);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Skipped);

        await using var check = NewContext();
        var updated = check.FflDealers.Single(d => d.Key == existing.Key);
        Assert.Equal("NEW NAME", updated.BusinessName);
        Assert.False(updated.AllowsTransfers); // store data survives an ATF refresh
        Assert.Equal(35.41, updated.Latitude); // same ZIP: position kept
        var added = check.FflDealers.Single(d => d.Key == "15620002");
        Assert.Equal("07", added.LicType);
        Assert.Null(added.Latitude);
        Assert.Equal("MAKER LLC", FflDirectory.DisplayName(added));
    }

    [Fact]
    public async Task Invoice_lists_both_shipments_of_a_mixed_order_and_renders_a_pdf()
    {
        ShippingRate = 10m;
        var session = Session();
        await AddToCart(session, SeedFirearm(500m));
        await AddToCart(session, SeedSellable(20m, "Sling"));
        var dealer = SeedDealer("10041", "CARTERS GUNSMITHING", 35.41, -80.58);
        await SelectDealer(session, dealer.Key);
        var result = await PlaceFor(session, expectedShipping: 20m);
        Assert.True(result.Success);

        await using var db = NewContext();
        var invoice = await new HKPartsEcommerce.Application.Features.Orders.GetOrderInvoiceQueryHandler(db)
            .Handle(new HKPartsEcommerce.Application.Features.Orders.GetOrderInvoiceQuery(result.OrderId), default);

        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(520m, invoice.SubTotal);
        Assert.Equal(20m, invoice.ShippingTotal);
        Assert.Equal("Alice Buyer", invoice.Shipping!.Name);
        Assert.Equal("Austin, TX 78701", invoice.Shipping.CityLine);
        Assert.False(invoice.ShipsToFflDealer);
        Assert.Equal("CARTERS GUNSMITHING", invoice.FflDealer!.Name);
        Assert.EndsWith("- $10.00", invoice.ShippingMethod);
        Assert.EndsWith("- $10.00", invoice.FflShippingMethod);
        Assert.Equal("Credit Card", invoice.PaymentMethod);
        Assert.Equal(invoice.Total, invoice.PaymentApplied);
        Assert.Equal(0m, invoice.AmountDue);

        var pdf = await new HKPartsEcommerce.Infrastructure.Documents.OrderDocumentService(db).GenerateInvoiceAsync(result.OrderId, default);
        Assert.True(pdf.Length > 1000);
        if (Environment.GetEnvironmentVariable("VSKY_INVOICE_PDF_OUT") is { Length: > 0 } outPath)
            await File.WriteAllBytesAsync(outPath, pdf);
    }

    private async Task<FflDealerDto> CreateDealer(string licenseNumber)
    {
        await using var db = NewContext();
        return await new CreateFflDealerCommandHandler(db, new NoGeocoder(), Clock, NullLogger<CreateFflDealerCommandHandler>.Instance)
            .Handle(new CreateFflDealerCommand(licenseNumber, "HAND ADDED LLC", "Hand Added Guns", "12 MAIN ST", "Concord", "nc",
                Zip, "(704) 555-0199", AllowsTransfers: true, LastTransferFee: 30m), default);
    }

    [Fact]
    public async Task Create_adds_a_manual_dealer_located_from_its_zip_and_searchable()
    {
        SeedZipCentre();

        var dto = await CreateDealer("1-56-025-01-9C-30001");

        Assert.Equal("15630001", dto.Key);
        Assert.True(dto.IsManual);
        Assert.True(dto.IsEligible);
        Assert.Equal("1-56-025-01-9C-30001", dto.LicenseNumber);
        await using var check = NewContext();
        var saved = check.FflDealers.Single(d => d.Key == "15630001");
        Assert.Equal(ZipLat, saved.Latitude);
        Assert.Equal("NC", saved.PremiseState);
        Assert.Equal("7045550199", saved.VoicePhone);
        Assert.Equal(30m, saved.LastTransferFee);

        var found = await Search(Zip, 15);
        Assert.Contains(found.Dealers, d => d.Key == "15630001");
    }

    [Fact]
    public async Task Create_refuses_a_licence_already_in_the_directory()
    {
        SeedDealer("30002", "LISTED", 35.41, -80.58);
        await Assert.ThrowsAsync<ConflictException>(() => CreateDealer("156025019C30002"));
    }

    [Theory]
    [InlineData("1-56-025-01-9C-30003", true)]
    [InlineData("156025019c30003", true)]
    [InlineData("1-56-025-01-9I-30003", false)] // no I month
    [InlineData("1-56-025-03-9C-30003", false)] // collector: can't receive transfers
    [InlineData("1-56-025-01-5A-30003", false)] // expired January 2025
    [InlineData("56-025-01-9C-30003", false)]
    public void Create_validates_the_ffl_number(string licenseNumber, bool valid)
    {
        var v = new CreateFflDealerCommandValidator(Clock);
        var result = v.Validate(new CreateFflDealerCommand(licenseNumber, "N", null, "1 A ST", "Concord", "NC", Zip, "7045550100"));
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public async Task Import_keeps_manual_dealers_active_and_confirms_matching_ones()
    {
        await CreateDealer("1-56-025-01-9C-30004"); // stays out of the file
        await CreateDealer("1-56-025-01-9C-30005"); // the file now lists it
        const string file =
            "LIC_REGN\tLIC_DIST\tLIC_CNTY\tLIC_TYPE\tLIC_XPRDTE\tLIC_SEQN\tLICENSE_NAME\tBUSINESS_NAME\tPREMISE_STREET\tPREMISE_CITY\tPREMISE_STATE\tPREMISE_ZIP_CODE\tVOICE_PHONE\n" +
            "1\t56\t025\t01\t9C\t30005\tATF NAME LLC\t\t12 MAIN ST\tCONCORD\tNC\t28025\t7045550199";

        await using (var db = NewContext())
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
            await new ImportFflDealersCommandHandler(db, Clock, NullLogger<ImportFflDealersCommandHandler>.Instance)
                .Handle(new ImportFflDealersCommand(stream, DeactivateMissing: true), default);
        }

        await using var check = NewContext();
        var kept = check.FflDealers.Single(d => d.Key == "15630004");
        Assert.True(kept.IsActive);
        Assert.True(kept.IsManual);
        var confirmed = check.FflDealers.Single(d => d.Key == "15630005");
        Assert.False(confirmed.IsManual);
        Assert.Equal("ATF NAME LLC", confirmed.LicenseName);
        Assert.Equal(30m, confirmed.LastTransferFee); // store data survives
    }

    private sealed class NoGeocoder : IZipGeocoder
    {
        public Task<bool> IsConfiguredAsync(CancellationToken ct) => Task.FromResult(false);
        public Task<GeoPoint?> GeocodeZipAsync(string zip5, CancellationToken ct) => Task.FromResult<GeoPoint?>(null);
    }
}
