using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A stored-value gift certificate. <see cref="CurrentBalance"/> only ever changes through the gift
/// certificate service, which writes a matching <see cref="GiftCertificateTransaction"/> in the same
/// transaction — so CurrentBalance always equals OriginalBalance + Σ(ledger entries after Created).
/// <see cref="OriginalBalance"/> never changes after creation.
/// </summary>
public class GiftCertificate : AuditableEntity, ISoftDeletable
{
    public Guid StoreId { get; set; }

    /// <summary>Stored upper-case; unique per store (case-insensitive by construction).</summary>
    public string Code { get; set; } = string.Empty;

    public decimal OriginalBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public GiftCertificateStatus Status { get; set; } = GiftCertificateStatus.Active;
    public GiftCertificateSource Source { get; set; } = GiftCertificateSource.Admin;

    /// <summary>The order that <em>purchased</em> this certificate (never an order that redeemed it).</summary>
    public Guid? SourceOrderId { get; set; }
    public Order? SourceOrder { get; set; }
    public Guid? SourceOrderItemId { get; set; }
    /// <summary>0-based unit within the source order line (one certificate per unit) — makes issuing idempotent.</summary>
    public int? SourceUnitIndex { get; set; }

    /// <summary>Emailed to the recipient (true) or a physical card mailed by staff (false).</summary>
    public bool IsOnline { get; set; } = true;

    public string? SenderName { get; set; }
    public string? SenderEmail { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Message { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }

    /// <summary>Optional; null never expires.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<GiftCertificateTransaction> Transactions { get; set; } = new List<GiftCertificateTransaction>();
}

/// <summary>Append-only ledger row: never updated or deleted.</summary>
public class GiftCertificateTransaction : BaseEntity
{
    public Guid GiftCertificateId { get; set; }
    public GiftCertificate? GiftCertificate { get; set; }
    public GiftCertificateAction Action { get; set; }
    /// <summary>Signed: + adds balance, − uses balance (0 for a status change).</summary>
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid? OrderId { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    /// <summary>The admin's email/name, or "system".</summary>
    public string CreatedBy { get; set; } = "system";
}
