using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Shipments;

/// <summary>A shipment of some/all of an order's lines, with carrier + tracking (REQ-ORD-002 / REQ-SHP-002).</summary>
public class ShipmentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public string? Carrier { get; set; }
    public string? ServiceName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? LabelPdfUrl { get; set; }
    public DateTime? ShippedOnUtc { get; set; }
    public DateTime? DeliveredOnUtc { get; set; }
    public List<ShipmentLineItemDto> Lines { get; set; } = new();
    public List<ShipmentTrackingDto> TrackingEvents { get; set; } = new();

    public static ShipmentDto From(Shipment s) => new()
    {
        Id = s.Id,
        OrderId = s.OrderId,
        ShipmentNumber = s.ShipmentNumber,
        Status = s.Status,
        Carrier = s.Carrier,
        ServiceName = s.ServiceName,
        TrackingNumber = s.TrackingNumber,
        LabelPdfUrl = s.LabelPdfUrl,
        ShippedOnUtc = s.ShippedOnUtc,
        DeliveredOnUtc = s.DeliveredOnUtc,
        Lines = s.Lines.Select(ShipmentLineItemDto.From).ToList(),
        TrackingEvents = s.TrackingEvents
            .OrderByDescending(t => t.CheckpointOnUtc)
            .Select(ShipmentTrackingDto.From)
            .ToList(),
    };
}

public class ShipmentLineItemDto
{
    public Guid Id { get; set; }
    public Guid OrderLineItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }

    public static ShipmentLineItemDto From(ShipmentLineItem l) => new()
    {
        Id = l.Id,
        OrderLineItemId = l.OrderLineItemId,
        ProductId = l.ProductId,
        ProductVariantId = l.ProductVariantId,
        ProductName = l.ProductName,
        Sku = l.Sku,
        Quantity = l.Quantity,
    };
}

public class ShipmentTrackingDto
{
    public string RawStatus { get; set; } = string.Empty;
    public ShipmentStatus NormalizedStatus { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime CheckpointOnUtc { get; set; }

    public static ShipmentTrackingDto From(ShipmentTracking t) => new()
    {
        RawStatus = t.RawStatus,
        NormalizedStatus = t.NormalizedStatus,
        Description = t.Description,
        Location = t.Location,
        CheckpointOnUtc = t.CheckpointOnUtc,
    };
}
