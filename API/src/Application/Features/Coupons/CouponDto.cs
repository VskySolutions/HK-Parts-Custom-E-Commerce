using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Coupons;

/// <summary>Full view of a redeemable coupon code bound to a discount (REQ-PRP-002).</summary>
public class CouponDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid DiscountId { get; set; }
    public CouponUsageType UsageType { get; set; }
    public int? MaxRedemptions { get; set; }
    public int RedemptionCount { get; set; }
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; }

    public static CouponDto From(CouponCode c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        DiscountId = c.DiscountId,
        UsageType = c.UsageType,
        MaxRedemptions = c.MaxRedemptions,
        RedemptionCount = c.RedemptionCount,
        StartDateUtc = c.StartDateUtc,
        EndDateUtc = c.EndDateUtc,
        IsActive = c.IsActive,
    };
}
