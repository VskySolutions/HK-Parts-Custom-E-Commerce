namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Moderation lifecycle of a customer-submitted product review (WO-14).</summary>
public enum ReviewStatus
{
    /// <summary>Submitted and awaiting admin moderation; not shown on the storefront.</summary>
    Pending = 0,

    /// <summary>Approved by an admin — visible on the storefront product page.</summary>
    Approved = 1,

    /// <summary>Rejected by an admin — never shown on the storefront.</summary>
    Rejected = 2
}
