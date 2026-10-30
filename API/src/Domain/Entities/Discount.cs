using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A configurable discount rule applied at cart/checkout time (REQ-PRP-001). Non-exclusive discounts
/// stack; an active exclusive discount overrides the others (AC-PRP-001.4).
/// </summary>
public class Discount : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public DiscountScope Scope { get; set; }
    public DiscountType Type { get; set; }

    /// <summary>Percentage (0–100) when <see cref="Type"/> is Percentage; otherwise a fixed reduction.</summary>
    public decimal Value { get; set; }

    /// <summary>Target when <see cref="Scope"/> is Product.</summary>
    public Guid? ProductId { get; set; }
    /// <summary>Target when <see cref="Scope"/> is Category.</summary>
    public Guid? CategoryId { get; set; }

    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public decimal? MinimumOrderValue { get; set; }

    public bool IsExclusive { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When true this discount is coupon-gated: the discount engine never applies it automatically —
    /// it only takes effect when a valid coupon code bound to it (see <see cref="CouponCodes"/>) is
    /// present on the cart/checkout (REQ-PRP-002). When false the rule auto-applies to every eligible cart.
    /// </summary>
    public bool RequiresCoupon { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<CouponCode> CouponCodes { get; set; } = new List<CouponCode>();
}
