using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Pricing;

/// <summary>
/// Validates and redeems coupon codes (REQ-PRP-002). A code is redeemable when it exists, is active,
/// falls within its valid-from/to window and has not exhausted its usage policy (single-use once,
/// limited up to its maximum, unlimited always). Redemption records a use and marks single-use codes
/// spent so they cannot be reused (AC-PRP-002.4).
/// </summary>
public class CouponService : ICouponService
{
    private readonly IApplicationDbContext _db;

    public CouponService(IApplicationDbContext db) => _db = db;

    public async Task<CouponValidationResult> ValidateAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new CouponValidationResult(false, null, "A coupon code is required.");

        var normalized = code.Trim();
        var coupon = await _db.CouponCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized, ct);

        if (coupon is null)
            return new CouponValidationResult(false, null, "The coupon code was not found.");
        if (!coupon.IsActive)
            return new CouponValidationResult(false, null, "The coupon is not active.");

        var now = DateTime.UtcNow;
        if (coupon.StartDateUtc.HasValue && coupon.StartDateUtc.Value > now)
            return new CouponValidationResult(false, null, "The coupon is not yet valid.");
        if (coupon.EndDateUtc.HasValue && coupon.EndDateUtc.Value < now)
            return new CouponValidationResult(false, null, "The coupon has expired.");

        if (IsExhausted(coupon))
            return new CouponValidationResult(false, null, "The coupon has been fully redeemed.");

        return new CouponValidationResult(true, coupon.DiscountId, null);
    }

    public async Task RedeemAsync(string code, CancellationToken ct = default)
    {
        var normalized = (code ?? string.Empty).Trim();
        var coupon = await _db.CouponCodes
            .FirstOrDefaultAsync(c => c.Code == normalized, ct)
            ?? throw new NotFoundException(nameof(CouponCode), code ?? string.Empty);

        coupon.RedemptionCount += 1;

        // Single-use codes are spent after their first redemption (AC-PRP-002.4).
        if (coupon.UsageType == CouponUsageType.SingleUse)
            coupon.IsActive = false;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Whether the code's usage policy leaves no further redemptions available.</summary>
    private static bool IsExhausted(CouponCode coupon) => coupon.UsageType switch
    {
        CouponUsageType.SingleUse => coupon.RedemptionCount >= 1,
        CouponUsageType.Limited => coupon.RedemptionCount >= (coupon.MaxRedemptions ?? 0),
        CouponUsageType.Unlimited => false,
        _ => true,
    };
}
