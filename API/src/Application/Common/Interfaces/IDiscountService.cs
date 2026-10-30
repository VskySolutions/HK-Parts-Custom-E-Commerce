using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Evaluates the configured discount rules against a cart (REQ-PRP-001). Non-exclusive discounts
/// stack; an applicable exclusive discount overrides all others, the single most-favorable one
/// winning (AC-PRP-001.4). Returns an itemized breakdown so callers can show buyers exactly which
/// promotions applied.
/// </summary>
public interface IDiscountService
{
    /// <summary>
    /// Computes the discounts that apply to the given cart lines and subtotal and their total
    /// reduction. Only active, in-window discounts whose minimum-order threshold is met are considered.
    /// A coupon-gated discount (<see cref="HKPartsEcommerce.Domain.Entities.Discount.RequiresCoupon"/>) is skipped
    /// unless its id appears in <paramref name="unlockedDiscountIds"/> — the ids of the discounts bound
    /// to the valid coupon codes on the cart (REQ-PRP-002).
    /// </summary>
    Task<DiscountEvaluationResult> EvaluateAsync(
        IReadOnlyList<DiscountCartLine> lines, decimal subtotal,
        IReadOnlyCollection<Guid>? unlockedDiscountIds = null, CancellationToken ct = default);
}
