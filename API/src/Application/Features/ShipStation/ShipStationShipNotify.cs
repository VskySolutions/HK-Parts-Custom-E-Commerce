using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ShipStation;

public enum ShipNotifyOutcome
{
    /// <summary>Items were shipped and a shipment recorded.</summary>
    Shipped,
    /// <summary>A second tracking number for items already shipped: a shipment with no items, no email (SS-43).</summary>
    AdditionalTracking,
    /// <summary>The same tracking number was already recorded: nothing changed (SS-43).</summary>
    Duplicate,
    /// <summary>No order with that number (SS-44: 404).</summary>
    NotFound,
    /// <summary>The order cannot be shipped (cancelled, or an inquiry): 409, logged.</summary>
    Rejected,
}

public class ShipNotifyResult
{
    public ShipNotifyOutcome Outcome { get; set; }
    public string? OrderNumber { get; set; }
    public string? Message { get; set; }
    public int ItemsShipped { get; set; }
}

/// <summary>The parts of a ShipStation <c>&lt;ShipNotice&gt;</c> body the site uses (SS-42).</summary>
public class ShipNotice
{
    public string? OrderNumber { get; set; }
    public string? Carrier { get; set; }
    public string? Service { get; set; }
    public string? TrackingNumber { get; set; }
    public string? ShipDate { get; set; }
    public decimal? ShippingCost { get; set; }
    public List<(string? LineItemId, string? Sku, int Quantity)> Items { get; set; } = new();

    /// <summary>Reads the body; null when it is empty or not XML. DTDs are refused (no external entities).</summary>
    public static ShipNotice? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var reader = XmlReader.Create(new StringReader(body),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var doc = XDocument.Load(reader);
            var root = doc.Root;
            if (root is null)
                return null;

            string? Value(XElement parent, string name) =>
                parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() is { Length: > 0 } v
                    ? v : null;

            var notice = new ShipNotice
            {
                OrderNumber = Value(root, "OrderNumber"),
                Carrier = Value(root, "Carrier"),
                Service = Value(root, "Service"),
                TrackingNumber = Value(root, "TrackingNumber"),
                ShipDate = Value(root, "ShipDate"),
                ShippingCost = decimal.TryParse(Value(root, "ShippingCost"), NumberStyles.Number, CultureInfo.InvariantCulture, out var cost)
                    ? cost : null,
            };
            var items = root.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Items", StringComparison.OrdinalIgnoreCase));
            if (items is not null)
                foreach (var item in items.Elements().Where(e => string.Equals(e.Name.LocalName, "Item", StringComparison.OrdinalIgnoreCase)))
                    notice.Items.Add((
                        Value(item, "LineItemID"),
                        Value(item, "SKU"),
                        int.TryParse(Value(item, "Quantity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) ? q : 0));
            return notice;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

/// <summary>
/// <c>POST /shipstation?action=shipnotify</c> (§3.5). Finds the order by number (an "-FFL" number ships only the
/// FFL group, SS-32), records a <see cref="Shipment"/> with <c>Source = "shipstation"</c>, the carrier, service,
/// tracking number, ship date and label cost (SS-42), moves the order to Shipped once every line has shipped
/// (Processing until then), and emails the buyer when "Email customer on shipnotify" is on (SS-40/46).
/// A repeat notice with the same tracking number changes nothing (SS-43). eBay orders get their tracking pushed
/// to eBay by the shipment interceptor (SS-45).
/// </summary>
public record ShipStationShipNotifyCommand(
    string? OrderNumber, string? Carrier, string? Service, string? TrackingNumber, string? Body) : IRequest<ShipNotifyResult>;

public class ShipStationShipNotifyCommandHandler : IRequestHandler<ShipStationShipNotifyCommand, ShipNotifyResult>
{
    public const string Source = "shipstation";

    private readonly IApplicationDbContext _db;
    private readonly IEmailEnqueuer _emails;
    private readonly IDateTimeProvider _clock;

    public ShipStationShipNotifyCommandHandler(IApplicationDbContext db, IEmailEnqueuer emails, IDateTimeProvider clock)
    {
        _db = db;
        _emails = emails;
        _clock = clock;
    }

    public async Task<ShipNotifyResult> Handle(ShipStationShipNotifyCommand request, CancellationToken ct)
    {
        var notice = ShipNotice.Parse(request.Body);
        var rawNumber = First(request.OrderNumber, notice?.OrderNumber);
        if (rawNumber is null)
            return new ShipNotifyResult { Outcome = ShipNotifyOutcome.NotFound, Message = "No order_number on the ship notice." };

        var (orderNumber, fflGroup) = ShipStationSupport.SplitOrderNumber(rawNumber);
        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.Lines)
            .Include(o => o.Shippings)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
        if (order is null)
            return new ShipNotifyResult { Outcome = ShipNotifyOutcome.NotFound, OrderNumber = rawNumber, Message = $"Order {rawNumber} was not found." };

        if (order.IsInquiry || order.Status == OrderStatus.Cancelled)
            return new ShipNotifyResult
            {
                Outcome = ShipNotifyOutcome.Rejected, OrderNumber = rawNumber,
                Message = order.IsInquiry ? "An inquiry cannot be shipped." : $"Order {rawNumber} is cancelled.",
            };

        var tracking = First(request.TrackingNumber, notice?.TrackingNumber);
        var carrier = First(request.Carrier, notice?.Carrier) ?? "ShipStation";
        var service = First(request.Service, notice?.Service);

        // SS-43: the same tracking number twice adds nothing.
        if (tracking is not null && await _db.Shipments.AnyAsync(s => s.OrderId == order.Id && s.TrackingNumber == tracking, ct))
            return new ShipNotifyResult { Outcome = ShipNotifyOutcome.Duplicate, OrderNumber = rawNumber };

        var settings = await ShipStationSupport.LoadAsync(_db, ct);
        var group = LinesInGroup(order, fflGroup, settings.SplitFfl);
        var toShip = PickQuantities(group, notice);

        var now = _clock.UtcNow;
        var zone = ShipStationSupport.ResolveTimeZone(settings.TimeZone);
        var shippedOn = ShipStationSupport.ParseLocalToUtc(notice?.ShipDate, zone) ?? now;

        var shipmentCount = await _db.Shipments.CountAsync(s => s.OrderId == order.Id, ct);
        var shipment = new Shipment
        {
            OrderId = order.Id,
            ShipmentNumber = $"{order.OrderNumber}-S{shipmentCount + 1}",
            Carrier = Truncate(carrier, 128),
            ServiceName = Truncate(service, 128),
            TrackingNumber = Truncate(tracking, 128),
            Status = ShipmentStatus.Shipped,
            ShippedOnUtc = shippedOn,
            Source = Source,
            ShippingCost = notice?.ShippingCost,
            Notes = fflGroup ? "FFL shipment (ShipStation)." : null,
        };

        var itemsShipped = 0;
        foreach (var (line, qty) in toShip)
        {
            line.QuantityShipped += qty;
            itemsShipped += qty;
            shipment.Lines.Add(new ShipmentLineItem
            {
                OrderLineItemId = line.Id,
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                Quantity = qty,
            });
        }
        _db.Shipments.Add(shipment);

        // SS-32 / FL-06: Shipped only once every shippable line has shipped; Processing until then.
        var fullyShipped = order.Lines.Where(ShipStationOrderBuilder.IsShippable).All(l => l.QuantityShipped >= l.Quantity);
        var from = order.Status;
        OrderStatus? to = null;
        if (fullyShipped && from is not (OrderStatus.Shipped or OrderStatus.Delivered))
            to = OrderStatus.Shipped;
        else if (!fullyShipped && itemsShipped > 0 && from is OrderStatus.Pending or OrderStatus.PendingRouting
                     or OrderStatus.Routed or OrderStatus.Accepted)
            to = OrderStatus.Processing;

        var comment = $"Shipped via ShipStation{(fflGroup ? " (FFL shipment)" : string.Empty)}: {carrier}" +
                      (tracking is null ? string.Empty : $" / Tracking Number {tracking}");
        if (to is OrderStatus target)
        {
            order.Status = target;
            if (target == OrderStatus.Shipped)
                order.ShippedOnUtc = shippedOn;
        }
        order.TrackingNumber ??= tracking;
        order.ShippingCarrier ??= carrier;

        // The order comment (SS-40): a history row, with or without a status change.
        _db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = from,
            ToStatus = order.Status,
            ChangedOnUtc = now,
            Note = Truncate(comment, 1000),
        });

        await _db.SaveChangesAsync(ct);

        if (itemsShipped > 0 && settings.NotifyCustomer && !string.IsNullOrWhiteSpace(order.ContactEmail))
            await _emails.EnqueueAsync(
                "OrderShipped",
                order.ContactEmail!,
                order.ContactName,
                $"A shipment for your order {order.OrderNumber} is on its way",
                $"Hi {order.ContactName},\n\n" +
                $"Shipment {shipment.ShipmentNumber} for order {order.OrderNumber} has been dispatched.\n" +
                $"Carrier: {carrier}\n" +
                (string.IsNullOrWhiteSpace(service) ? string.Empty : $"Service: {service}\n") +
                (string.IsNullOrWhiteSpace(tracking) ? string.Empty : $"Tracking number: {tracking}\n") +
                "\nThank you for shopping with us.",
                cancellationToken: ct);

        return new ShipNotifyResult
        {
            Outcome = itemsShipped > 0 ? ShipNotifyOutcome.Shipped : ShipNotifyOutcome.AdditionalTracking,
            OrderNumber = rawNumber,
            ItemsShipped = itemsShipped,
        };
    }

    /// <summary>The lines a ShipStation order number covers: "-FFL" = the FFL lines; the plain number of a split order = the rest.</summary>
    public static List<OrderLineItem> LinesInGroup(Order order, bool fflGroup, bool splitFfl)
    {
        var shippable = order.Lines.Where(ShipStationOrderBuilder.IsShippable).ToList();
        if (fflGroup)
            return shippable.Where(l => l.IsFfl).ToList();

        var isSplit = splitFfl
                      && order.Shippings.Any(s => s.ShipmentType == OrderShipmentType.Ffl)
                      && shippable.Any(l => l.IsFfl) && shippable.Any(l => !l.IsFfl);
        return isSplit ? shippable.Where(l => !l.IsFfl).ToList() : shippable;
    }

    /// <summary>
    /// What to ship: the notice's items matched by LineItemID (else SKU) within the group, capped at what is
    /// still unshipped; with no usable items, everything unshipped in the group.
    /// </summary>
    public static List<(OrderLineItem Line, int Quantity)> PickQuantities(List<OrderLineItem> group, ShipNotice? notice)
    {
        var picked = new Dictionary<Guid, int>();
        if (notice is { Items.Count: > 0 })
        {
            foreach (var (lineItemId, sku, quantity) in notice.Items)
            {
                if (quantity <= 0) continue;
                var line = Guid.TryParse(lineItemId, out var id)
                    ? group.FirstOrDefault(l => l.Id == id)
                    : null;
                line ??= sku is null ? null : group.FirstOrDefault(l =>
                    string.Equals(l.Sku, sku, StringComparison.OrdinalIgnoreCase)
                    && l.Quantity - l.QuantityShipped - picked.GetValueOrDefault(l.Id) > 0);
                if (line is null) continue;
                var remaining = line.Quantity - line.QuantityShipped - picked.GetValueOrDefault(line.Id);
                if (remaining > 0)
                    picked[line.Id] = picked.GetValueOrDefault(line.Id) + Math.Min(quantity, remaining);
            }
        }

        if (picked.Count == 0)
            foreach (var line in group.Where(l => l.QuantityShipped < l.Quantity))
                picked[line.Id] = line.Quantity - line.QuantityShipped;

        return group.Where(l => picked.ContainsKey(l.Id)).Select(l => (l, picked[l.Id])).ToList();
    }

    private static string? First(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
