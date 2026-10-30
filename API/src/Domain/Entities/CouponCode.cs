using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A redeemable code bound to a <see cref="Discount"/> (REQ-PRP-002). Single-use codes are marked
/// redeemed on order completion; limited codes track a redemption count against a maximum.
/// </summary>
public class CouponCode : AuditableEntity, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;

    public Guid DiscountId { get; set; }
    public Discount? Discount { get; set; }

    public CouponUsageType UsageType { get; set; }

    /// <summary>Maximum redemptions when <see cref="UsageType"/> is Limited.</summary>
    public int? MaxRedemptions { get; set; }
    public int RedemptionCount { get; set; }

    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
