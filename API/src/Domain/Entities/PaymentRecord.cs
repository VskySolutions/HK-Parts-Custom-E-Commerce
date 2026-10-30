using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A payment attempt/record against an <see cref="Order"/>, tracking the full authorize → capture →
/// refund lifecycle (REQ-PAY-001/002/003). Gateway references are stored for later capture/refund.
/// </summary>
public class PaymentRecord : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public PaymentMethodType Method { get; set; }
    public string? GatewayName { get; set; }

    /// <summary>
    /// The payment instrument within the gateway, when the gateway supports more than one on the same
    /// credential — currently "BankAccount" for an Authorize.Net ACH/eCheck payment, else null/"Card".
    /// Persisted because eCheck and card diverge on capture/refund (ACH is capture-only, and an eCheck
    /// refund references a bank account rather than a credit card) — the stored value tells the later
    /// capture/refund which path to take. See <see cref="Common.PaymentInstruments"/>.
    /// </summary>
    public string? PaymentInstrument { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal RefundedAmount { get; set; }

    public string? AuthorizationId { get; set; }
    public string? TransactionId { get; set; }
    public string? GatewayReference { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime? AuthorizedOnUtc { get; set; }
    public DateTime? CapturedOnUtc { get; set; }
    public DateTime? RefundedOnUtc { get; set; }

    /// <summary>When an authorization-only hold expires; used to flag orders for manual review (AC-PAY-002.4).</summary>
    public DateTime? AuthorizationExpiresUtc { get; set; }
}
