namespace HKPartsEcommerce.Domain.Enums;

/// <summary>What a discount applies to (REQ-PRP-001).</summary>
public enum DiscountScope
{
    CartTotal = 0,
    Product = 1,
    Category = 2,
    OrderSubtotal = 3
}

/// <summary>How a discount reduces price (AC-PRP-001.2).</summary>
public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

/// <summary>Coupon redemption policy (AC-PRP-002.1).</summary>
public enum CouponUsageType
{
    SingleUse = 0,
    Limited = 1,
    Unlimited = 2
}
