using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A customer's running loyalty-points balance (WO-27). One row per customer (unique CustomerId). The
/// balance is maintained as points are earned on orders, redeemed at checkout, or clawed back on refund;
/// every mutation also appends an immutable <see cref="PointsTransaction"/> so the balance is auditable.
/// Never negative.
/// </summary>
public class CustomerPointsBalance : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Current spendable points balance (never negative).</summary>
    public int Balance { get; set; }
}
