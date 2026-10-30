using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A buyer-initiated return (RMA) against a delivered order (REQ-ORD-004). Created in
/// <see cref="RmaStatus.Requested"/>; an admin/store manager approves or rejects it and records the
/// resolution (refund / replacement / store credit). On an approved refund the original gateway is used
/// and accepted units are restocked.
/// </summary>
public class Rma : AuditableEntity, ISoftDeletable
{
    public string RmaNumber { get; set; } = string.Empty;

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>The requesting customer (plain reference; buyer scoping is by this id).</summary>
    public Guid CustomerId { get; set; }

    public RmaStatus Status { get; set; } = RmaStatus.Requested;
    public RmaResolution Resolution { get; set; } = RmaResolution.None;

    public string Reason { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }

    public DateTime RequestedOnUtc { get; set; }
    public DateTime? ResolvedOnUtc { get; set; }
    public Guid? ResolvedById { get; set; }
    public decimal? RefundedAmount { get; set; }

    /// <summary>Store credit issued to the buyer when resolved with <see cref="RmaResolution.StoreCredit"/> (WO-48).</summary>
    public decimal? StoreCreditIssued { get; set; }
    /// <summary>The replacement order created when resolved with <see cref="RmaResolution.Replacement"/> (WO-48).</summary>
    public Guid? ReplacementOrderId { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<RmaLineItem> Lines { get; set; } = new List<RmaLineItem>();
}

/// <summary>A quantity of one order line included in an <see cref="Rma"/> (snapshotted product data).</summary>
public class RmaLineItem : BaseEntity
{
    public Guid RmaId { get; set; }
    public Rma? Rma { get; set; }

    public Guid OrderLineItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public string? LineReason { get; set; }
}
