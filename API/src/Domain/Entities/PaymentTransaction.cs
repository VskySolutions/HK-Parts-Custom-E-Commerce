using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// One gateway operation against a <see cref="PaymentRecord"/>: the sale/authorization, each capture, void and
/// (partial) refund. The parent record keeps the running state (status, refunded total); these rows keep the
/// history, including failures, so the order page can show what was sent and what the gateway answered
/// (Authorize.Net AN-25/26/27/42). The parent's <see cref="PaymentRecord.TransactionId"/> always stays the
/// original transaction — refunds reference it, so it must never be overwritten by a refund's own id.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    public Guid PaymentRecordId { get; set; }
    public PaymentRecord? PaymentRecord { get; set; }

    public PaymentTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public bool Success { get; set; }

    /// <summary>The gateway's id for this operation (a refund gets its own id).</summary>
    public string? TransactionId { get; set; }
    /// <summary>The original transaction this operation referenced (capture / void / refund).</summary>
    public string? RefTransactionId { get; set; }

    public string? ResponseCode { get; set; }
    public string? ReasonCode { get; set; }
    /// <summary>Gateway response text (admin-facing).</summary>
    public string? Message { get; set; }

    /// <summary>Admin who triggered it, when not the checkout itself.</summary>
    public Guid? CreatedById { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}
