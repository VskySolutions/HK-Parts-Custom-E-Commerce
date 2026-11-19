using System.Text;
using System.Xml;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Features.ShipStation;

/// <summary>One page of the export: the XML document and what went into it (for the call log).</summary>
public class ShipStationExportResult
{
    public string Xml { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int Pages { get; set; }
    public int Page { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    /// <summary>SKUs exported with no weight (SS-29), so the product data can be fixed.</summary>
    public List<string> MissingWeightSkus { get; set; } = new();
}

/// <summary>
/// <c>GET /shipstation?action=export&amp;start_date&amp;end_date&amp;page</c> (§3.2): the paid orders of the
/// chosen statuses whose last change falls inside the window, 100 per page (SS-24), as ShipStation Custom
/// Store XML built with an XML writer (SS-16). Dates are store local time (SS-14) in MM/dd/yyyy HH:mm
/// (SS-23); money has no thousands separator (SS-22). A mixed FFL order becomes two ShipStation orders when
/// "Split mixed FFL orders" is on (SS-31); an FFL-only order ships to the dealer (SS-30).
/// Pickup orders, inquiries and orders with nothing to ship are never exported.
/// </summary>
public record ShipStationExportQuery(string? StartDate, string? EndDate, int? Page) : IRequest<ShipStationExportResult>;

public class ShipStationExportQueryHandler : IRequestHandler<ShipStationExportQuery, ShipStationExportResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ShipStationExportQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ShipStationExportResult> Handle(ShipStationExportQuery request, CancellationToken ct)
    {
        var settings = await ShipStationSupport.LoadAsync(_db, ct);
        var zone = ShipStationSupport.ResolveTimeZone(settings.TimeZone);

        var startUtc = ShipStationSupport.ParseLocalToUtc(request.StartDate, zone);
        var endUtc = string.IsNullOrWhiteSpace(request.EndDate) ? _clock.UtcNow : ShipStationSupport.ParseLocalToUtc(request.EndDate, zone);
        if (startUtc is null || endUtc is null)
            throw new ValidationException(new[]
            {
                new ValidationFailure("start_date", $"start_date and end_date must be dates in {ShipStationSupport.DateFormat} format."),
            });

        // Minute precision: an order changed during the end minute is still inside the window.
        var endInclusive = endUtc.Value.AddSeconds(59);

        var names = ShipStationSupport.ParseStatuses(settings.ExportStatuses);
        var openStatuses = names
            .Where(n => n is not ShipStationStatuses.Cancelled and not ShipStationStatuses.OnHold)
            .SelectMany(ShipStationStatuses.OrderStatusesFor)
            .ToList();
        var includeCancelled = names.Contains(ShipStationStatuses.Cancelled);
        var includeOnHold = names.Contains(ShipStationStatuses.OnHold);
        var paid = ShipStationStatuses.Paid;
        var paidThenCancelled = ShipStationStatuses.PaidThenCancelled;

        var query = _db.Orders.AsNoTracking()
            .Where(o => !o.IsInquiry && !o.IsPickup
                        && o.UpdatedOnUtc >= startUtc.Value && o.UpdatedOnUtc <= endInclusive)
            .Where(o => (openStatuses.Contains(o.Status) && paid.Contains(o.PaymentStatus))
                        || (includeCancelled && o.Status == OrderStatus.Cancelled && paidThenCancelled.Contains(o.PaymentStatus))
                        || (includeOnHold && o.Status != OrderStatus.Cancelled && o.PaymentStatus == PaymentStatus.HeldForReview));
        if (!settings.IncludeEbay)
            query = query.Where(o => o.EbayOrderId == null);

        var total = await query.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)ShipStationSupport.PageSize));
        var page = Math.Clamp(request.Page ?? 1, 1, pages);

        var orders = await query
            .OrderBy(o => o.PlacedOnUtc).ThenBy(o => o.Id)
            .Skip((page - 1) * ShipStationSupport.PageSize)
            .Take(ShipStationSupport.PageSize)
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .Include(o => o.Shippings)
            .Include(o => o.Payments)
            .AsSplitQuery()
            .ToListAsync(ct);

        var catalog = await LoadCatalogAsync(orders, ct);
        var result = new ShipStationExportResult { Page = page, Pages = pages, StartUtc = startUtc.Value, EndUtc = endUtc.Value };

        var parts = orders.SelectMany(o => ShipStationOrderBuilder.Split(o, settings.SplitFfl)).ToList();
        result.OrderCount = parts.Count;
        result.Xml = ShipStationOrderBuilder.Write(parts, pages, zone, catalog, result.MissingWeightSkus);
        return result;
    }

    private async Task<ShipStationCatalog> LoadCatalogAsync(List<Order> orders, CancellationToken ct)
    {
        var lines = orders.SelectMany(o => o.Lines).ToList();
        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var variantIds = lines.Where(l => l.ProductVariantId is not null).Select(l => l.ProductVariantId!.Value).Distinct().ToList();

        var products = await _db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Weight })
            .ToDictionaryAsync(p => p.Id, p => p.Weight, ct);

        var variants = await _db.ProductVariants.AsNoTracking().IgnoreQueryFilters()
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new
            {
                v.Id,
                v.Weight,
                Options = v.AttributeValues
                    .Select(av => new { av.ProductAttributeValue!.ProductAttribute!.Name, av.ProductAttributeValue.Value, av.ProductAttributeValue.ProductAttribute.DisplayOrder })
                    .ToList(),
            })
            .ToListAsync(ct);

        return new ShipStationCatalog(
            products,
            variants.ToDictionary(v => v.Id, v => v.Weight),
            variants.ToDictionary(v => v.Id, v => (IReadOnlyList<(string, string)>)v.Options
                .Where(o => !string.IsNullOrWhiteSpace(o.Name))
                .OrderBy(o => o.DisplayOrder)
                .Select(o => (o.Name, o.Value))
                .ToList()));
    }
}

/// <summary>Product data the export reads: weights (lb) and variant option names/values.</summary>
public record ShipStationCatalog(
    IReadOnlyDictionary<Guid, decimal?> ProductWeights,
    IReadOnlyDictionary<Guid, decimal?> VariantWeights,
    IReadOnlyDictionary<Guid, IReadOnlyList<(string Name, string Value)>> VariantOptions);

/// <summary>One ShipStation order: the whole site order, or one half of a split FFL order.</summary>
public record ShipStationOrderPart(
    Order Order,
    string OrderNumber,
    string OrderId,
    IReadOnlyList<OrderLineItem> Lines,
    OrderShipping? Shipping,
    bool ShipToDealer,
    bool IsFflHalf,
    decimal ShippingAmount,
    decimal TaxAmount,
    decimal OrderTotal,
    IReadOnlyList<string> InternalNotes);

/// <summary>Turns site orders into ShipStation orders and writes the XML (pure: unit-tested without a database).</summary>
public static class ShipStationOrderBuilder
{
    /// <summary>Online gift certificates are emailed, never shipped.</summary>
    public static bool IsShippable(OrderLineItem line) => string.IsNullOrWhiteSpace(line.GcRecipientEmail);

    public static IReadOnlyList<ShipStationOrderPart> Split(Order order, bool splitFfl)
    {
        var shippable = order.Lines.Where(IsShippable).ToList();
        if (shippable.Count == 0)
            return Array.Empty<ShipStationOrderPart>();

        var fflRow = order.Shippings.FirstOrDefault(s => s.ShipmentType == OrderShipmentType.Ffl);
        var customerRow = order.Shippings.FirstOrDefault(s => s.ShipmentType != OrderShipmentType.Ffl);
        var fflLines = shippable.Where(l => l.IsFfl).ToList();
        var otherLines = shippable.Where(l => !l.IsFfl).ToList();

        // SS-31: mixed order → <number> to the customer and <number>-FFL to the dealer.
        if (splitFfl && fflRow is not null && fflLines.Count > 0 && otherLines.Count > 0)
        {
            var fflTotal = fflLines.Sum(l => l.LineTotal) + fflRow.ShippingTotal;
            var customerShipping = customerRow?.ShippingTotal ?? Math.Max(0m, order.ShippingTotal - fflRow.ShippingTotal);
            return new[]
            {
                new ShipStationOrderPart(order, order.OrderNumber, order.Id.ToString(), otherLines, customerRow,
                    ShipToDealer: false, IsFflHalf: false, customerShipping, order.TaxTotal, order.TotalAmount - fflTotal,
                    Notes(order, customerRow, toDealer: false, extra: $"FFL items ship separately as {order.OrderNumber}{ShipStationSupport.FflSuffix}.")),
                new ShipStationOrderPart(order, order.OrderNumber + ShipStationSupport.FflSuffix, order.Id + ShipStationSupport.FflSuffix,
                    fflLines, fflRow, ShipToDealer: true, IsFflHalf: true, fflRow.ShippingTotal, 0m, fflTotal,
                    Notes(order, fflRow, toDealer: true)),
            };
        }

        // SS-30: FFL-only order → the dealer.
        if (fflRow is not null && otherLines.Count == 0)
            return new[]
            {
                new ShipStationOrderPart(order, order.OrderNumber, order.Id.ToString(), shippable, fflRow,
                    ShipToDealer: true, IsFflHalf: false, order.ShippingTotal, order.TaxTotal, order.TotalAmount,
                    Notes(order, fflRow, toDealer: true)),
            };

        var extra = fflRow is not null
            ? $"Contains FFL items: ship them to {fflRow.FflDealerName} (FFL {fflRow.FflNumber}), not to the customer."
            : null;
        return new[]
        {
            new ShipStationOrderPart(order, order.OrderNumber, order.Id.ToString(), shippable, customerRow,
                ShipToDealer: false, IsFflHalf: false, order.ShippingTotal, order.TaxTotal, order.TotalAmount,
                Notes(order, customerRow, toDealer: false, extra)),
        };
    }

    private static IReadOnlyList<string> Notes(Order order, OrderShipping? row, bool toDealer, string? extra = null)
    {
        var notes = new List<string>();
        if (toDealer && row is not null)
            notes.Add($"FFL — ship to dealer: {row.FflDealerName}, FFL {row.FflNumber}.");
        if (row?.SignatureRequired == true)
            notes.Add("Signature required.");
        if (row?.InsuranceSelected == true)
            notes.Add($"Insurance: declared value {ShipStationSupport.Money(row.DeclaredValue)}.");
        if (row?.AddressVerifyStatus == AddressVerificationStatus.Unavailable)
            notes.Add("Address not verified (verification service unavailable at checkout).");
        if (row?.AddressOverridden == true)
            notes.Add("Customer kept their own address over the verified suggestion.");
        if (!string.IsNullOrWhiteSpace(order.AppliedCouponCode))
            notes.Add($"Coupon: {order.AppliedCouponCode}.");
        if (!string.IsNullOrWhiteSpace(extra))
            notes.Add(extra);
        return notes;
    }

    public static string Write(
        IReadOnlyList<ShipStationOrderPart> parts, int pages, TimeZoneInfo zone, ShipStationCatalog catalog, List<string> missingWeightSkus)
    {
        using var stream = new MemoryStream();
        var xmlSettings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using (var w = XmlWriter.Create(stream, xmlSettings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("Orders");
            w.WriteAttributeString("pages", pages.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var part in parts)
                WriteOrder(w, part, zone, catalog, missingWeightSkus);
            w.WriteEndElement();
            w.WriteEndDocument();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteOrder(XmlWriter w, ShipStationOrderPart part, TimeZoneInfo zone, ShipStationCatalog catalog, List<string> missingWeightSkus)
    {
        var o = part.Order;
        var fullyShipped = part.Lines.All(l => l.QuantityShipped >= l.Quantity);
        var status = o.Status == OrderStatus.Cancelled
            ? "cancelled"
            : fullyShipped ? "shipped" : ShipStationStatuses.ShipStationStatus(o.Status, o.PaymentStatus);

        w.WriteStartElement("Order");
        Element(w, "OrderID", part.OrderId);
        Element(w, "OrderNumber", part.OrderNumber);
        Element(w, "OrderDate", ShipStationSupport.FormatLocal(o.PlacedOnUtc, zone));
        Element(w, "OrderStatus", status);
        Element(w, "LastModified", ShipStationSupport.FormatLocal(o.UpdatedOnUtc, zone));
        Element(w, "ShippingMethod", part.Shipping?.DisplayName ?? o.ShippingMethodName);
        Element(w, "PaymentMethod", PaymentMethodName(o));
        Element(w, "CurrencyCode", o.CurrencyCode);
        Element(w, "OrderTotal", ShipStationSupport.Money(part.OrderTotal));
        Element(w, "TaxAmount", ShipStationSupport.Money(part.TaxAmount));
        Element(w, "ShippingAmount", ShipStationSupport.Money(part.ShippingAmount));
        // SS-26: the eBay buyer's message for eBay orders, the checkout comment for site orders.
        Element(w, "CustomerNotes", !string.IsNullOrWhiteSpace(o.EbayOrderId) ? o.EbayBuyerMessage : o.CustomerNote);
        Element(w, "InternalNotes", part.InternalNotes.Count > 0 ? string.Join(" ", part.InternalNotes) : null);

        var buyer = o.ShippingAddress;
        var billing = o.BillingAddress ?? buyer;
        var email = buyer?.Email ?? billing?.Email;

        w.WriteStartElement("Customer");
        Element(w, "CustomerCode", email);
        w.WriteStartElement("BillTo");
        Element(w, "Name", FullName(billing));
        Element(w, "Company", billing?.Company ?? o.CompanyName);
        Element(w, "Phone", billing?.PhoneNumber ?? buyer?.PhoneNumber);
        Element(w, "Email", billing?.Email ?? email);
        w.WriteEndElement();

        w.WriteStartElement("ShipTo");
        if (part.ShipToDealer && part.Shipping is { } dealer)
        {
            Element(w, "Name", dealer.FflDealerName);
            Element(w, "Company", dealer.FflDealerName);
            Element(w, "Address1", dealer.FflDealerStreet);
            Element(w, "Address2", null);
            Element(w, "City", dealer.FflDealerCity);
            Element(w, "State", dealer.FflDealerState);
            Element(w, "PostalCode", dealer.FflDealerZip);
            Element(w, "Country", "US");
            Element(w, "Phone", dealer.FflDealerPhone);
        }
        else
        {
            Element(w, "Name", FullName(buyer));
            Element(w, "Company", buyer?.Company);
            Element(w, "Address1", buyer?.AddressLine1);
            Element(w, "Address2", buyer?.AddressLine2);
            Element(w, "City", buyer?.City);
            Element(w, "State", buyer?.StateProvince);
            Element(w, "PostalCode", buyer?.PostalCode);
            Element(w, "Country", buyer?.CountryCode);
            Element(w, "Phone", buyer?.PhoneNumber);
        }
        w.WriteEndElement(); // ShipTo
        w.WriteEndElement(); // Customer

        w.WriteStartElement("Items");
        var itemsTotal = 0m;
        foreach (var line in part.Lines)
        {
            itemsTotal += line.UnitPrice * line.Quantity;
            WriteItem(w, line, catalog, missingWeightSkus);
        }

        // SS-27: discounts, coupons, gift certificates, points and fees as one adjustment item, so the items
        // plus tax and shipping add up to OrderTotal.
        var adjustment = decimal.Round(part.OrderTotal - itemsTotal - part.TaxAmount - part.ShippingAmount, 2);
        if (adjustment != 0m)
        {
            w.WriteStartElement("Item");
            Element(w, "SKU", adjustment < 0 ? "DISCOUNT" : "FEE");
            Element(w, "Name", adjustment < 0 ? "Discounts and credits" : "Fees");
            Element(w, "Quantity", "1");
            Element(w, "UnitPrice", ShipStationSupport.Money(adjustment));
            Element(w, "Adjustment", "true");
            w.WriteEndElement();
        }
        w.WriteEndElement(); // Items

        w.WriteEndElement(); // Order
    }

    private static void WriteItem(XmlWriter w, OrderLineItem line, ShipStationCatalog catalog, List<string> missingWeightSkus)
    {
        decimal? weight = null;
        if (line.ProductVariantId is Guid variantId && catalog.VariantWeights.TryGetValue(variantId, out var vw))
            weight = vw;
        if (weight is null && catalog.ProductWeights.TryGetValue(line.ProductId, out var pw))
            weight = pw;
        if (weight is null or <= 0m)
            missingWeightSkus.Add(line.Sku ?? line.ProductName);

        w.WriteStartElement("Item");
        Element(w, "LineItemID", line.Id.ToString());
        Element(w, "SKU", line.Sku);
        Element(w, "Name", line.ProductName);
        Element(w, "Weight", ShipStationSupport.Weight(weight ?? 0m));
        Element(w, "WeightUnits", "Pounds");
        Element(w, "Quantity", line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Element(w, "UnitPrice", ShipStationSupport.Money(line.UnitPrice));

        // SS-20: the variant's options and the buyer's typed-in values; no <Options> when there are none.
        var options = new List<(string Name, string Value)>();
        if (line.ProductVariantId is Guid vid && catalog.VariantOptions.TryGetValue(vid, out var variantOptions))
            options.AddRange(variantOptions);
        options.AddRange(CustomAttributes.Parse(line.CustomAttributesJson).Select(a => (a.Name, a.Value)));
        if (options.Count > 0)
        {
            w.WriteStartElement("Options");
            foreach (var (name, value) in options)
            {
                w.WriteStartElement("Option");
                Element(w, "Name", name);
                Element(w, "Value", value);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static string? FullName(Address? a) =>
        a is null ? null : $"{a.FirstName} {a.LastName}".Trim() is { Length: > 0 } n ? n : null;

    private static string? PaymentMethodName(Order o)
    {
        var payment = o.Payments.Where(p => p.IsMaster).OrderByDescending(p => p.CreatedOnUtc).FirstOrDefault()
                      ?? o.Payments.OrderByDescending(p => p.CreatedOnUtc).FirstOrDefault();
        if (payment is null)
            return o.EbayOrderId is not null ? "eBay" : null;
        return payment.Method switch
        {
            PaymentMethodType.AuthorizeNet => "Credit Card",
            PaymentMethodType.Stripe or PaymentMethodType.Square => "Credit Card",
            PaymentMethodType.CashOnDelivery => "Cash on Delivery",
            PaymentMethodType.BankTransfer => "Bank Transfer",
            PaymentMethodType.GiftCertificate => "Gift Certificate",
            PaymentMethodType.NoPaymentRequired => "No Payment Required",
            _ => payment.Method.ToString(),
        };
    }

    /// <summary>Writes an element with escaped text (SS-16: "&amp;", "&lt;" and "]]&gt;" cannot break the document).</summary>
    private static void Element(XmlWriter w, string name, string? value)
    {
        w.WriteStartElement(name);
        if (!string.IsNullOrEmpty(value))
            w.WriteString(XmlSafe(value));
        w.WriteEndElement();
    }

    /// <summary>Drops characters XML 1.0 cannot carry at all (control characters pasted into a name).</summary>
    private static string XmlSafe(string value)
    {
        static bool Keep(char c) => XmlConvert.IsXmlChar(c) || char.IsSurrogate(c);
        return value.All(Keep) ? value : new string(value.Where(Keep).ToArray());
    }
}
