using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A physical shipment of some (or all) of an order's lines (REQ-ORD-002). Carries carrier/label/tracking
/// data (REQ-SHP-002) so partial shipments (WO-46) and carrier label generation + tracking sync (WO-42)
/// share one aggregate. An order can have several shipments; a line's shipped quantity is tracked on the
/// <see cref="OrderLineItem"/>.
/// </summary>
public class Shipment : AuditableEntity, ISoftDeletable
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Human-friendly, unique shipment reference.</summary>
    public string ShipmentNumber { get; set; } = string.Empty;

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Created;

    public string? Carrier { get; set; }
    public string? ServiceName { get; set; }
    public string? TrackingNumber { get; set; }

    // Carrier label (WO-42): stored asset + resolvable URL.
    public string? LabelAssetKey { get; set; }
    public string? LabelPdfUrl { get; set; }
    public DateTime? LabelGeneratedOnUtc { get; set; }

    public DateTime? ShippedOnUtc { get; set; }
    public DateTime? DeliveredOnUtc { get; set; }
    public DateTime? LastPolledOnUtc { get; set; }

    public string? Notes { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<ShipmentLineItem> Lines { get; set; } = new List<ShipmentLineItem>();
    public ICollection<ShipmentTracking> TrackingEvents { get; set; } = new List<ShipmentTracking>();
}

/// <summary>A quantity of one order line included in a <see cref="Shipment"/> (snapshotted product data).</summary>
public class ShipmentLineItem : BaseEntity
{
    public Guid ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    /// <summary>Plain reference back to the shipped <see cref="OrderLineItem"/> (no FK — order lines are snapshots).</summary>
    public Guid OrderLineItemId { get; set; }

    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
}

/// <summary>An append-only carrier tracking checkpoint for a <see cref="Shipment"/> (AC-SHP-002.3).</summary>
public class ShipmentTracking : BaseEntity
{
    public Guid ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    public string RawStatus { get; set; } = string.Empty;
    public ShipmentStatus NormalizedStatus { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime CheckpointOnUtc { get; set; }
    public DateTime RecordedOnUtc { get; set; }
}
