namespace HKPartsEcommerce.Domain.Enums;

/// <summary>How a customer group adjusts prices for its members (REQ-CUS-003).</summary>
public enum CustomerGroupPricingRuleType
{
    /// <summary>No price adjustment — members pay the base price.</summary>
    None = 0,

    /// <summary>A flat percentage off the base price (DiscountPercent) — AC-CUS-003.3.</summary>
    PercentageDiscount = 1,

    /// <summary>Explicit per-product/variant group prices (CustomerGroupPrice rows) — AC-CUS-003.4.</summary>
    FixedGroupPrice = 2
}

/// <summary>Lifecycle of a customer-submitted tax exemption request (REQ-TAX-003).</summary>
public enum TaxExemptionRequestStatus
{
    /// <summary>Submitted and awaiting admin review; the customer remains taxable (AC-TAX-003.3).</summary>
    PendingReview = 0,

    /// <summary>Approved — the customer is marked tax-exempt (AC-TAX-003.5).</summary>
    Approved = 1,

    /// <summary>Rejected — the customer remains taxable and may submit a new request (AC-TAX-003.6).</summary>
    Rejected = 2
}
