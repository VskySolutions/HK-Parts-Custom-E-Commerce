using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A return merchandise authorization against one order (docs/order-return-requirements.md). Moves through
/// <see cref="RmaStatus"/> (requested → approved → in transit → received → inspected → refunded/exchanged →
/// closed); every change is written to <see cref="History"/>. Money only moves at the refund step, and only
/// after the gateway has accepted it (RT-61).
/// </summary>
public class Rma : AuditableEntity, ISoftDeletable
{
    /// <summary>RMA-&lt;order number&gt;-&lt;n&gt; (RT-24).</summary>
    public string RmaNumber { get; set; } = string.Empty;

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>The requesting customer (plain reference; buyer scoping is by this id). Null for a guest order.</summary>
    public Guid? CustomerId { get; set; }

    public RmaStatus Status { get; set; } = RmaStatus.Requested;
    /// <summary>When <see cref="Status"/> last changed — drives the "age in status" column (RT-31).</summary>
    public DateTime? StatusChangedOnUtc { get; set; }

    /// <summary>What the customer asked for on the request form.</summary>
    public RmaResolution RequestedResolution { get; set; } = RmaResolution.Refund;
    /// <summary>What was actually done; None until refunded / exchanged.</summary>
    public RmaResolution Resolution { get; set; } = RmaResolution.None;

    public RmaReturnMethod ReturnMethod { get; set; } = RmaReturnMethod.SelfShip;
    public RmaSource Source { get; set; } = RmaSource.Customer;

    /// <summary>The customer's overall comment (per-line reasons live on the lines).</summary>
    public string Reason { get; set; } = string.Empty;
    /// <summary>Customer-visible note from staff on reject / close (e.g. the rejection reason).</summary>
    public string? ResolutionNotes { get; set; }

    // ---- Restrictions (RT-14 / RT-15 / RT-17) ----
    /// <summary>At least one line is a firearm: dealer details are required before approval, no self-service label.</summary>
    public bool RequiresFfl { get; set; }
    /// <summary>At least one line cannot go back by carrier (hazmat / freight): label step is skipped.</summary>
    public bool ShipRestricted { get; set; }
    public string? FflDealerName { get; set; }
    public string? FflNumber { get; set; }
    public DateTime? FflExpiry { get; set; }
    /// <summary>Set when staff overrode an eligibility rule; the reason is also in the history (RT-17).</summary>
    public string? EligibilityOverrideReason { get; set; }

    // ---- Return shipping (section 8) ----
    public string? LabelCarrier { get; set; }
    public string? LabelService { get; set; }
    public string? TrackingNumber { get; set; }
    /// <summary>Link to the label PDF (carrier URL or an uploaded file's URL).</summary>
    public string? LabelUrl { get; set; }
    public decimal? LabelCost { get; set; }
    /// <summary>An approved RMA with no shipment by this time expires (RT-04).</summary>
    public DateTime? LabelExpiresAtUtc { get; set; }
    /// <summary>When the "return expiring" email went out, so it is sent once.</summary>
    public DateTime? ExpiryWarningSentOnUtc { get; set; }
    /// <summary>Free-text shipping instructions shown to the customer (restricted items, drop-off).</summary>
    public string? ReturnInstructions { get; set; }

    // ---- Refund breakdown (section 10), recomputed at inspection and frozen at refund ----
    public decimal SubtotalRefund { get; set; }
    public decimal TaxRefund { get; set; }
    public decimal ShippingRefund { get; set; }
    public decimal RestockFee { get; set; }
    public decimal LabelDeduction { get; set; }
    /// <summary>Signed manual override (RT-59).</summary>
    public decimal Adjustment { get; set; }
    public string? AdjustmentReason { get; set; }
    public decimal TotalRefund { get; set; }
    /// <summary>Staff chose to refund the original shipping (otherwise only on a whole-order merchant-fault return).</summary>
    public bool RefundShipping { get; set; }
    /// <summary>Staff waived the restocking fee for this return.</summary>
    public bool WaiveRestockFee { get; set; }

    public RmaRefundMethod RefundMethod { get; set; } = RmaRefundMethod.None;
    /// <summary>Gateway transaction ids, or the manual refund reference staff typed.</summary>
    public string? RefundReference { get; set; }
    /// <summary>Kept for compatibility: the amount refunded to the original payment.</summary>
    public decimal? RefundedAmount { get; set; }
    /// <summary>Store credit issued (gift certificate value incl. any bonus).</summary>
    public decimal? StoreCreditIssued { get; set; }
    public Guid? StoreCreditGiftCertificateId { get; set; }
    public string? StoreCreditCode { get; set; }
    /// <summary>The replacement order created by an exchange.</summary>
    public Guid? ReplacementOrderId { get; set; }

    /// <summary>Number of refund attempts; part of the gateway idempotency key (RT-67).</summary>
    public int RefundAttempts { get; set; }
    /// <summary>Set while a refund is calling the gateway, so a double click / second admin is refused (RT-67).</summary>
    public DateTime? RefundStartedOnUtc { get; set; }
    /// <summary>The last gateway error, shown on the RMA while it stays Inspected (RT-61).</summary>
    public string? LastRefundError { get; set; }

    // ---- Timestamps for reporting ----
    public DateTime RequestedOnUtc { get; set; }
    public DateTime? ApprovedOnUtc { get; set; }
    public DateTime? ReceivedOnUtc { get; set; }
    public DateTime? RefundedOnUtc { get; set; }
    public DateTime? ClosedOnUtc { get; set; }
    public DateTime? ResolvedOnUtc { get; set; }
    public Guid? ResolvedById { get; set; }
    public Guid? CreatedByAdminId { get; set; }

    /// <summary>Optimistic-concurrency token: two staff acting on the same RMA at once cannot both win.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<RmaLineItem> Lines { get; set; } = new List<RmaLineItem>();
    public ICollection<RmaHistory> History { get; set; } = new List<RmaHistory>();
    public ICollection<RmaMessage> Messages { get; set; } = new List<RmaMessage>();
    public ICollection<RmaAttachment> Attachments { get; set; } = new List<RmaAttachment>();
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

    /// <summary>Quantity requested / authorised.</summary>
    public int Quantity { get; set; }
    /// <summary>Quantity physically received (may exceed <see cref="Quantity"/>: "received, not authorised").</summary>
    public int QuantityReceived { get; set; }
    /// <summary>Quantity accepted for refund after inspection.</summary>
    public int QuantityAccepted { get; set; }
    /// <summary>Units of this line put back into stock.</summary>
    public int QuantityRestocked { get; set; }

    public Guid? ReasonId { get; set; }
    /// <summary>Snapshot of the reason name (survives the reason being renamed or removed).</summary>
    public string? LineReason { get; set; }
    public string? CustomerComment { get; set; }
    public RmaItemCondition? ConditionReported { get; set; }
    public RmaReceivedCondition? ConditionReceived { get; set; }
    public RmaDisposition? Disposition { get; set; }

    /// <summary>The unit price actually paid, after pro-rating order-level discounts (RT-56).</summary>
    public decimal UnitRefund { get; set; }
    /// <summary>Inspection deduction (used, missing parts …) for the whole line (RT-54).</summary>
    public decimal Deduction { get; set; }
    public string? DeductionReason { get; set; }
    /// <summary>QuantityAccepted × UnitRefund − Deduction.</summary>
    public decimal LineRefund { get; set; }
    /// <summary>Copied from the reason so a later reason edit does not change a settled return.</summary>
    public bool FeeApplies { get; set; }
    public bool MerchantFault { get; set; }
}

/// <summary>A return reason managed in admin (section 6.2 / 15).</summary>
public class RmaReason : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    /// <summary>Shown on the customer form (an inactive or hidden reason is still usable by staff).</summary>
    public bool CustomerVisible { get; set; } = true;
    /// <summary>The restocking fee applies to lines with this reason.</summary>
    public bool FeeApplies { get; set; }
    /// <summary>The store is at fault: label deduction and restocking fee are waived, shipping may be refunded.</summary>
    public bool MerchantFault { get; set; }
    /// <summary>Eligible for auto-approval (RT-25).</summary>
    public bool AutoApprove { get; set; }
    /// <summary>A customer comment is required.</summary>
    public bool RequiresComment { get; set; }
    /// <summary>A photo is required when the "require photos" setting is on.</summary>
    public bool RequiresPhoto { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>One status change (or skipped step) on an RMA (RT-02). Immutable.</summary>
public class RmaHistory : BaseEntity
{
    public Guid RmaId { get; set; }
    public Rma? Rma { get; set; }
    public RmaStatus? FromStatus { get; set; }
    public RmaStatus ToStatus { get; set; }
    public RmaActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? Note { get; set; }
    /// <summary>Money-moving or override actions record the caller's IP (RT-98).</summary>
    public string? IpAddress { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}

/// <summary>A message on the RMA thread. Internal notes are never shown or emailed to the customer (RT-35).</summary>
public class RmaMessage : BaseEntity
{
    public Guid RmaId { get; set; }
    public Rma? Rma { get; set; }
    public RmaActorType AuthorType { get; set; }
    public Guid? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}

/// <summary>A photo the customer (or staff) attached; stored privately and streamed through an authorised route (RT-27).</summary>
public class RmaAttachment : BaseEntity
{
    public Guid RmaId { get; set; }
    public Rma? Rma { get; set; }
    public Guid? RmaLineItemId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public RmaActorType UploadedByType { get; set; }
    public Guid? UploadedById { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}
