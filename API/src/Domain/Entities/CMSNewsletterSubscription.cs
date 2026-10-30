using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A storefront newsletter subscriber with a subscription lifecycle (WO-56). CMS-owned: table
/// name is <c>CMSNewsletterSubscriptions</c>.</summary>
public class CMSNewsletterSubscription : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public NewsletterSubscriptionStatus Status { get; set; } = NewsletterSubscriptionStatus.Pending;

    public DateTime? ConfirmedOnUtc { get; set; }
    public DateTime? UnsubscribedOnUtc { get; set; }

    /// <summary>Where the subscription originated (e.g. "footer", "home-strip", "checkout").</summary>
    public string? Source { get; set; }
}
