namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// Outcome of validating a coupon code for redemption (REQ-PRP-002). When <see cref="IsValid"/> is
/// <c>true</c>, <see cref="DiscountId"/> identifies the discount the code unlocks; otherwise
/// <see cref="Error"/> explains why the code was rejected.
/// </summary>
public record CouponValidationResult(bool IsValid, Guid? DiscountId, string? Error);
