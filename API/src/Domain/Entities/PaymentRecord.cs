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

    /// <summary>The payment that settles the order (false for a partial gift-certificate payment alongside a card).</summary>
    public bool IsMaster { get; set; } = true;

    public DateTime? AuthorizedOnUtc { get; set; }
    public DateTime? CapturedOnUtc { get; set; }
    public DateTime? RefundedOnUtc { get; set; }

    /// <summary>When an authorization-only hold expires; used to flag orders for manual review (AC-PAY-002.4).</summary>
    public DateTime? AuthorizationExpiresUtc { get; set; }

    /// <summary>
    /// The amount the gateway authorized at checkout. <see cref="Amount"/> becomes the captured (settled) amount
    /// once a lower capture is taken, so this keeps the ceiling a capture may never exceed (AN-21).
    /// </summary>
    public decimal? AuthorizedAmount { get; set; }

    // ---- Gateway response detail (Authorize.Net AN-18; null for gateways that do not return it) ----

    /// <summary>Issuer approval code.</summary>
    public string? AuthCode { get; set; }
    /// <summary>AVS (address verification) result code, e.g. "Y", "Z", "N".</summary>
    public string? AvsResultCode { get; set; }
    /// <summary>Card code (CVV) verification result, e.g. "M" match, "N" no match.</summary>
    public string? CvvResultCode { get; set; }
    /// <summary>Card network transaction id.</summary>
    public string? NetworkTransactionId { get; set; }
    /// <summary>Card brand / account type the gateway reported, e.g. "Visa", "MasterCard", "eCheck".</summary>
    public string? CardType { get; set; }
    /// <summary>Last four digits of the card / account (never the full number).</summary>
    public string? CardLastFour { get; set; }

    /// <summary>Raw gateway result: responseCode (1 approved, 2 declined, 3 error, 4 held for review).</summary>
    public string? GatewayResponseCode { get; set; }
    /// <summary>Raw gateway reason / error code, e.g. "2", "27", "E00027". Admin-only.</summary>
    public string? GatewayReasonCode { get; set; }
    /// <summary>Raw gateway response text, kept for the admin; the buyer only sees a friendly message (AN-19).</summary>
    public string? GatewayResponseText { get; set; }

    /// <summary>Authorize.Net CIM customer profile id created from this transaction (AN-30).</summary>
    public string? CustomerProfileId { get; set; }
    /// <summary>Authorize.Net CIM payment profile id: the chargeable stored card (AN-31).</summary>
    public string? CustomerPaymentProfileId { get; set; }

    /// <summary>Every gateway operation on this payment (sale, capture, void, refund), oldest first (AN-25/26/42).</summary>
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}
