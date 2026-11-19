using System.Xml.Linq;
using HKPartsEcommerce.Application.Features.ShipStation;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Shipping;

/// <summary>
/// docs/shipstation-verification-requirements.md §7: the P.O. box rule (PO-T*), the export XML (SS-T5..14) and the
/// ship notice (SS-T15..18). The XML builder is pure; the handlers run against the test SQL Server database.
/// </summary>
public class ShipStationTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 18, 22, 34, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo NewYork = ShipStationSupport.ResolveTimeZone("America/New_York");
    private static readonly ShipStationCatalog EmptyCatalog = new(
        new Dictionary<Guid, decimal?>(), new Dictionary<Guid, decimal?>(), new Dictionary<Guid, IReadOnlyList<(string, string)>>());

    // ---------------------------------------------------------------- P.O. box rule (AV-32)

    [Theory]
    [InlineData("PO Box 12")]
    [InlineData("P.O. Box 12")]
    [InlineData("p o box 12")]
    [InlineData("Post Office Box 12")]
    [InlineData("POB 12")]
    [InlineData("P.O.B. 12")]
    [InlineData("Box 12")]
    [InlineData("Postal Box 9")]
    [InlineData("Lock Box 400")]
    [InlineData("POBox12")]
    [InlineData("P Box 77")]
    public void PoT1_po_boxes_are_caught(string line) => Assert.True(PoBoxDetector.IsPoBox(line));

    [Theory]
    [InlineData("12 Box Elder St")]
    [InlineData("40 Boxwood Ln")]
    [InlineData("1 Post Rd")]
    [InlineData("3334 Gray Moss Rd")]
    [InlineData("88 Pobst Ave")]
    [InlineData("Box Canyon Road 5")]
    [InlineData("Suite 100")]
    public void PoT2_street_addresses_pass(string line) => Assert.False(PoBoxDetector.IsPoBox(line));

    // ---------------------------------------------------------------- export XML (pure)

    private static Order NewOrder(string number = "100045")
    {
        var order = new Order
        {
            OrderNumber = number,
            Status = OrderStatus.Processing,
            PaymentStatus = PaymentStatus.Captured,
            PlacedOnUtc = Now,
            UpdatedOnUtc = Now,
            CurrencyCode = "USD",
            ShippingAddress = new Address
            {
                FirstName = "Dana", LastName = "Reed", Email = "dana@example.test", PhoneNumber = "5550100",
                AddressLine1 = "3334 Gray Moss Rd", City = "Charlotte", StateProvince = "NC", PostalCode = "28270", CountryCode = "US",
            },
        };
        return order;
    }

    private static OrderLineItem Line(string sku, int qty, decimal price, bool ffl = false, string? custom = null) => new()
    {
        ProductId = Guid.NewGuid(), Sku = sku, ProductName = sku + " item", Quantity = qty, UnitPrice = price,
        LineTotal = price * qty, IsFfl = ffl, CustomAttributesJson = custom,
    };

    private static XDocument Export(params Order[] orders) => Export(true, orders);

    private static XDocument Export(bool splitFfl, params Order[] orders)
    {
        var parts = orders.SelectMany(o => ShipStationOrderBuilder.Split(o, splitFfl)).ToList();
        var xml = ShipStationOrderBuilder.Write(parts, 1, NewYork, EmptyCatalog, new List<string>());
        return XDocument.Parse(xml);
    }

    [Fact]
    public void SsT5_money_has_no_thousands_separator()
    {
        var order = NewOrder();
        order.Lines.Add(Line("A", 1, 10000m));
        order.TaxTotal = 1250m;
        order.ShippingTotal = 1500m;
        order.TotalAmount = 12750m;

        var o = Export(order).Root!.Element("Order")!;
        Assert.Equal("1250.00", o.Element("TaxAmount")!.Value);
        Assert.Equal("1500.00", o.Element("ShippingAmount")!.Value);
        Assert.Equal("12750.00", o.Element("OrderTotal")!.Value);
        Assert.Equal("10000.00", o.Descendants("UnitPrice").First().Value);
    }

    [Fact]
    public void SsT6_dates_are_24_hour_local_time_without_am_pm()
    {
        var order = NewOrder();
        order.Lines.Add(Line("A", 1, 5m));
        order.TotalAmount = 5m;

        // 22:34 UTC = 17:34 in New York (EST).
        Assert.Equal("11/18/2026 17:34", Export(order).Root!.Element("Order")!.Element("OrderDate")!.Value);
    }

    [Fact]
    public void SsT7_and_T8_options_are_exported_only_when_present()
    {
        var order = NewOrder();
        order.Lines.Add(Line("ENGRAVED", 1, 5m, custom: "[{\"attributeId\":\"" + Guid.NewGuid() + "\",\"name\":\"Color\",\"value\":\"Black\"}]"));
        order.Lines.Add(Line("PLAIN", 1, 5m));
        order.TotalAmount = 10m;

        var items = Export(order).Descendants("Item").ToList();
        var engraved = items.Single(i => i.Element("SKU")!.Value == "ENGRAVED");
        var option = engraved.Element("Options")!.Element("Option")!;
        Assert.Equal("Color", option.Element("Name")!.Value);
        Assert.Equal("Black", option.Element("Value")!.Value);
        Assert.Null(items.Single(i => i.Element("SKU")!.Value == "PLAIN").Element("Options"));
    }

    [Fact]
    public void SsT12_special_characters_keep_the_xml_valid()
    {
        var order = NewOrder();
        var line = Line("A&B", 1, 5m);
        line.ProductName = "Smith & Wesson <M&P> ]]> grip";
        order.Lines.Add(line);
        order.TotalAmount = 5m;
        order.CustomerNote = "Leave at <door> & ring ]]>";

        var doc = Export(order);
        Assert.Equal("Smith & Wesson <M&P> ]]> grip", doc.Descendants("Name").First(n => n.Parent!.Name == "Item").Value);
        Assert.Equal("Leave at <door> & ring ]]>", doc.Root!.Element("Order")!.Element("CustomerNotes")!.Value);
    }

    private static OrderShipping FflRow(decimal shipping) => new()
    {
        ShipmentType = OrderShipmentType.Ffl, ShipToType = ShipToType.FflDealer, ShippingTotal = shipping,
        DisplayName = "UPS Ground", FflDealerName = "Queen City Arms", FflNumber = "1-56-071-01-2B-12345",
        FflDealerStreet = "100 Trade St", FflDealerCity = "Charlotte", FflDealerState = "NC", FflDealerZip = "28202",
        ContainsFflItems = true,
    };

    [Fact]
    public void SsT13_ffl_only_order_ships_to_the_dealer()
    {
        var order = NewOrder();
        order.HasFflItems = true;
        order.Lines.Add(Line("RIFLE", 1, 800m, ffl: true));
        order.Shippings.Add(FflRow(25m));
        order.ShippingTotal = 25m;
        order.TotalAmount = 825m;

        var o = Export(order).Root!.Elements("Order").Single();
        Assert.Equal("100045", o.Element("OrderNumber")!.Value);
        var shipTo = o.Element("Customer")!.Element("ShipTo")!;
        Assert.Equal("Queen City Arms", shipTo.Element("Company")!.Value);
        Assert.Equal("100 Trade St", shipTo.Element("Address1")!.Value);
        Assert.Contains("1-56-071-01-2B-12345", o.Element("InternalNotes")!.Value);
    }

    [Fact]
    public void SsT14_mixed_order_exports_two_orders()
    {
        var order = NewOrder();
        order.HasFflItems = true;
        order.Lines.Add(Line("RIFLE", 1, 800m, ffl: true));
        order.Lines.Add(Line("SLING", 2, 20m));
        order.Shippings.Add(new OrderShipping { ShipmentType = OrderShipmentType.NonFfl, ShippingTotal = 8m, DisplayName = "USPS" });
        order.Shippings.Add(FflRow(25m));
        order.ShippingTotal = 33m;
        order.TaxTotal = 6m;
        order.TotalAmount = 800m + 40m + 33m + 6m;

        var orders = Export(order).Root!.Elements("Order").ToList();
        Assert.Equal(2, orders.Count);

        var customer = orders.Single(o => o.Element("OrderNumber")!.Value == "100045");
        Assert.Equal("SLING", customer.Descendants("SKU").Single().Value);
        Assert.Equal("3334 Gray Moss Rd", customer.Element("Customer")!.Element("ShipTo")!.Element("Address1")!.Value);
        Assert.Equal("54.00", customer.Element("OrderTotal")!.Value); // 40 items + 8 shipping + 6 tax

        var ffl = orders.Single(o => o.Element("OrderNumber")!.Value == "100045-FFL");
        Assert.Equal("RIFLE", ffl.Descendants("SKU").Single().Value);
        Assert.Equal("Queen City Arms", ffl.Element("Customer")!.Element("ShipTo")!.Element("Name")!.Value);
        Assert.Equal("825.00", ffl.Element("OrderTotal")!.Value);
    }

    [Fact]
    public void SsT27_discounts_become_one_adjustment_item()
    {
        var order = NewOrder();
        order.Lines.Add(Line("A", 2, 50m));
        order.ShippingTotal = 10m;
        order.DiscountTotal = 15m;
        order.TotalAmount = 95m;

        var adjustment = Export(order).Descendants("Item").Single(i => i.Element("Adjustment") is not null);
        Assert.Equal("-15.00", adjustment.Element("UnitPrice")!.Value);
    }

    // ---------------------------------------------------------------- handlers (database)

    private async Task SeedSettingsAsync(bool includeEbay = false)
    {
        await using var db = NewContext();
        db.ShipStationSettings.Add(new ShipStationSettings
        {
            Enabled = true, Username = "ss-user", Password = "ss-password-1", IncludeEbay = includeEbay,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Order> SeedOrderAsync(string number, Action<Order>? configure = null)
    {
        await using var db = NewContext();
        var order = NewOrder(number);
        order.Lines.Add(Line("SKU-" + number, 1, 10m));
        order.TotalAmount = 10m;
        configure?.Invoke(order);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private const string WideStart = "01/01/2000 00:00";
    private const string WideEnd = "12/31/2099 23:59";

    [Fact]
    public async Task SsT10_export_pages_by_100()
    {
        await SeedSettingsAsync();
        await using (var db = NewContext())
        {
            for (var i = 0; i < 105; i++)
            {
                var o = NewOrder($"PG{i:000}-{Guid.NewGuid():N}"[..20]);
                o.PlacedOnUtc = Now.AddMinutes(i);
                o.Lines.Add(Line("P" + i, 1, 1m));
                o.TotalAmount = 1m;
                db.Orders.Add(o);
            }
            await db.SaveChangesAsync();
        }

        await using var ctx = NewContext();
        var handler = new ShipStationExportQueryHandler(ctx, new FixedClock(Now));
        var page2 = await handler.Handle(new ShipStationExportQuery(WideStart, WideEnd, 2), default);

        Assert.Equal(2, page2.Pages);
        Assert.Equal(5, page2.OrderCount);
        Assert.Equal("2", XDocument.Parse(page2.Xml).Root!.Attribute("pages")!.Value);
    }

    [Fact]
    public async Task SsT11_ebay_orders_are_skipped_unless_enabled_and_unpaid_orders_never_go()
    {
        await SeedSettingsAsync(includeEbay: false);
        var site = await SeedOrderAsync("SITE-1");
        await SeedOrderAsync("EBAY-1", o => o.EbayOrderId = "12-34567-89012");
        await SeedOrderAsync("UNPAID-1", o => o.PaymentStatus = PaymentStatus.AwaitingPayment);
        await SeedOrderAsync("PICKUP-1", o => o.IsPickup = true);

        await using var ctx = NewContext();
        var result = await new ShipStationExportQueryHandler(ctx, new FixedClock(Now))
            .Handle(new ShipStationExportQuery(WideStart, WideEnd, 1), default);

        var numbers = XDocument.Parse(result.Xml).Descendants("OrderNumber").Select(n => n.Value).ToList();
        Assert.Contains(site.OrderNumber, numbers);
        Assert.DoesNotContain("EBAY-1", numbers);
        Assert.DoesNotContain("UNPAID-1", numbers);
        Assert.DoesNotContain("PICKUP-1", numbers);
    }

    [Fact]
    public async Task SsT19_cancelled_paid_orders_are_exported_as_cancelled()
    {
        await SeedSettingsAsync();
        await SeedOrderAsync("CXL-1", o => { o.Status = OrderStatus.Cancelled; o.PaymentStatus = PaymentStatus.Refunded; });

        await using var ctx = NewContext();
        var result = await new ShipStationExportQueryHandler(ctx, new FixedClock(Now))
            .Handle(new ShipStationExportQuery(WideStart, WideEnd, 1), default);

        var order = XDocument.Parse(result.Xml).Descendants("Order").Single(o => o.Element("OrderNumber")!.Value == "CXL-1");
        Assert.Equal("cancelled", order.Element("OrderStatus")!.Value);
    }

    [Fact]
    public async Task SsT2_T3_T4_authentication()
    {
        await using (var db = NewContext())
        {
            db.ShipStationSettings.Add(new ShipStationSettings { Enabled = false, Username = "ss-user", Password = "ss-password-1" });
            await db.SaveChangesAsync();
        }

        async Task<ShipStationAuthResult> Auth(string? user, string? pass)
        {
            await using var ctx = NewContext();
            return (await new AuthenticateShipStationQueryHandler(ctx, new FixedClock(Now))
                .Handle(new AuthenticateShipStationQuery(user, pass, false), default)).Result;
        }

        Assert.Equal(ShipStationAuthResult.Disabled, await Auth("ss-user", "ss-password-1")); // SS-T3

        await using (var db = NewContext())
        {
            var s = await db.ShipStationSettings.SingleAsync();
            s.Enabled = true;
            await db.SaveChangesAsync();
        }

        Assert.Equal(ShipStationAuthResult.Ok, await Auth("ss-user", "ss-password-1"));
        Assert.Equal(ShipStationAuthResult.BadCredentials, await Auth("ss-user", "wrong"));           // SS-T2
        Assert.Equal(ShipStationAuthResult.MissingCredentials, await Auth(null, null));                // SS-T4
    }

    private async Task<ShipNotifyResult> NotifyAsync(FakeEmailEnqueuer emails, string orderNumber, string tracking, string? body = null)
    {
        await using var ctx = NewContext();
        return await new ShipStationShipNotifyCommandHandler(ctx, emails, new FixedClock(Now))
            .Handle(new ShipStationShipNotifyCommand(orderNumber, "UPS", "Ground", tracking, body), default);
    }

    [Fact]
    public async Task SsT16_T17_ship_notice_ships_once_and_emails_once()
    {
        await SeedSettingsAsync();
        var order = await SeedOrderAsync("SHIP-1");
        var emails = new FakeEmailEnqueuer();

        var first = await NotifyAsync(emails, "SHIP-1", "1Z999");
        var second = await NotifyAsync(emails, "SHIP-1", "1Z999");

        Assert.Equal(ShipNotifyOutcome.Shipped, first.Outcome);
        Assert.Equal(ShipNotifyOutcome.Duplicate, second.Outcome);
        Assert.Equal(1, emails.SentCount);

        await using var ctx = NewContext();
        var saved = await ctx.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Shipped, saved.Status);
        var shipment = await ctx.Shipments.SingleAsync(s => s.OrderId == order.Id);
        Assert.Equal("shipstation", shipment.Source);
        Assert.Equal("1Z999", shipment.TrackingNumber);
        Assert.Contains(await ctx.OrderStatusHistory.Where(h => h.OrderId == order.Id).Select(h => h.Note).ToListAsync(),
            n => n != null && n.Contains("1Z999"));
    }

    [Fact]
    public async Task SsT43_second_tracking_number_adds_a_shipment_without_email()
    {
        await SeedSettingsAsync();
        var order = await SeedOrderAsync("SHIP-2");
        var emails = new FakeEmailEnqueuer();

        await NotifyAsync(emails, "SHIP-2", "1ZAAA");
        var extra = await NotifyAsync(emails, "SHIP-2", "1ZBBB");

        Assert.Equal(ShipNotifyOutcome.AdditionalTracking, extra.Outcome);
        Assert.Equal(1, emails.SentCount);
        await using var ctx = NewContext();
        Assert.Equal(2, await ctx.Shipments.CountAsync(s => s.OrderId == order.Id));
    }

    [Fact]
    public async Task SsT18_unknown_order_is_not_found()
    {
        await SeedSettingsAsync();
        var result = await NotifyAsync(new FakeEmailEnqueuer(), "NO-SUCH-ORDER", "1Z000");
        Assert.Equal(ShipNotifyOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task SsT15_ffl_group_ships_alone_and_order_stays_processing()
    {
        await SeedSettingsAsync();
        var order = await SeedOrderAsync("MIX-1", o =>
        {
            o.Lines.Clear();
            o.HasFflItems = true;
            o.Lines.Add(Line("RIFLE", 1, 800m, ffl: true));
            o.Lines.Add(Line("SLING", 1, 20m));
            o.Shippings.Add(new OrderShipping { ShipmentType = OrderShipmentType.NonFfl, ShippingTotal = 8m });
            o.Shippings.Add(FflRow(25m));
        });
        var emails = new FakeEmailEnqueuer();

        var result = await NotifyAsync(emails, "MIX-1-FFL", "1ZFFL");

        Assert.Equal(ShipNotifyOutcome.Shipped, result.Outcome);
        Assert.Equal(1, result.ItemsShipped);
        Assert.Equal(1, emails.SentCount);
        await using var ctx = NewContext();
        var saved = await ctx.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Processing, saved.Status);
        Assert.Equal(1, saved.Lines.Single(l => l.Sku == "RIFLE").QuantityShipped);
        Assert.Equal(0, saved.Lines.Single(l => l.Sku == "SLING").QuantityShipped);
    }

    [Fact]
    public void Ship_notice_body_is_parsed_and_dtds_are_refused()
    {
        var notice = ShipNotice.Parse(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><ShipNotice><OrderNumber>100045</OrderNumber><Carrier>USPS</Carrier>" +
            "<Service>Priority</Service><TrackingNumber>9400</TrackingNumber><ShipDate>11/18/2026</ShipDate><ShippingCost>8.25</ShippingCost>" +
            "<Items><Item><SKU>SLING</SKU><Quantity>2</Quantity></Item></Items></ShipNotice>");
        Assert.NotNull(notice);
        Assert.Equal("9400", notice!.TrackingNumber);
        Assert.Equal(8.25m, notice.ShippingCost);
        Assert.Equal(2, notice.Items.Single().Quantity);

        Assert.Null(ShipNotice.Parse("<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///c:/windows/win.ini\">]><ShipNotice>&e;</ShipNotice>"));
    }
}
