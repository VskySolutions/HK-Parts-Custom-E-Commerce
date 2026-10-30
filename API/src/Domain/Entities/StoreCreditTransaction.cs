using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// One immutable entry in a customer's store-credit ledger (WO-48, REQ-ORD-004). A customer's balance is
/// the sum of the signed <see cref="Amount"/> values: issues and positive adjustments add, redemptions and
/// negative adjustments subtract. Issued as a resolution when a return is approved for store credit.
/// </summary>
public class StoreCreditTransaction : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Signed amount: positive credits the balance, negative debits it.</summary>
    public decimal Amount { get; set; }
    public StoreCreditTransactionType Type { get; set; }
    public string CurrencyCode { get; set; } = "USD";

    public string? Reason { get; set; }

    /// <summary>The return this credit was issued for, when applicable.</summary>
    public Guid? RmaId { get; set; }
    /// <summary>The order this entry relates to (the returned order, or the order credit was spent on).</summary>
    public Guid? OrderId { get; set; }
}
