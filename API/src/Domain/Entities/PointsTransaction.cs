using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// One immutable entry in a customer's loyalty-points ledger (WO-27). <see cref="Points"/> is signed:
/// positive credits the balance (earning, positive adjustment), negative debits it (redemption, refund
/// reversal, negative adjustment). <see cref="BalanceAfter"/> snapshots the resulting balance so the
/// ledger reads as a running statement without re-summing.
/// </summary>
public class PointsTransaction : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Signed points delta: positive earns, negative redeems/reverses.</summary>
    public int Points { get; set; }

    public PointsTransactionType Type { get; set; }

    /// <summary>Human-readable note for the entry (e.g. "Points earned on order.").</summary>
    public string? Reason { get; set; }

    /// <summary>The order this entry relates to (earned on / redeemed against / reversed for), when applicable.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>The customer's points balance immediately after this entry was applied.</summary>
    public int BalanceAfter { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
