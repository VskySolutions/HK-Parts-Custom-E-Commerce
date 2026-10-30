using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Validates and redeems coupon codes bound to discounts (REQ-PRP-002). Validation checks existence,
/// active state, the valid-from/to window and the usage policy; redemption records a use and marks
/// single-use codes as spent (AC-PRP-002.4).
/// </summary>
public interface ICouponService
{
    /// <summary>
    /// Checks whether <paramref name="code"/> may currently be redeemed and, if so, which discount it
    /// unlocks. Never throws for an unknown/invalid code — the reason is returned in the result.
    /// </summary>
    Task<CouponValidationResult> ValidateAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Records a redemption of <paramref name="code"/> (increments the count and marks single-use codes
    /// redeemed), called on order completion (AC-PRP-002.4).
    /// </summary>
    Task RedeemAsync(string code, CancellationToken ct = default);
}
