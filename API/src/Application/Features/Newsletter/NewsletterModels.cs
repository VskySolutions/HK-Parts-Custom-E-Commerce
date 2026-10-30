using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Newsletter;

/// <summary>Admin list/export view of a newsletter subscriber, annotated with marketing-suppression state (WO-56).</summary>
public class NewsletterSubscriptionDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public NewsletterSubscriptionStatus Status { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ConfirmedOnUtc { get; set; }

    /// <summary>True when this subscriber's email is present in the marketing-suppression list (from
    /// <see cref="MarketingSuppression"/>). Read-only here; suppression is written by WO-87.</summary>
    public bool IsSuppressed { get; set; }

    public static NewsletterSubscriptionDto From(CMSNewsletterSubscription e, bool isSuppressed) => new()
    {
        Id = e.Id,
        Email = e.Email,
        Status = e.Status,
        Source = e.Source,
        CreatedOnUtc = e.CreatedOnUtc,
        ConfirmedOnUtc = e.ConfirmedOnUtc,
        IsSuppressed = isSuppressed,
    };
}

/// <summary>Result of a public newsletter subscribe attempt. Always a success (the storefront form treats
/// it as such); <see cref="AlreadySubscribed"/> distinguishes a fresh subscribe from an idempotent no-op.</summary>
public class SubscribeResultDto
{
    public string Email { get; set; } = string.Empty;
    public NewsletterSubscriptionStatus Status { get; set; }

    /// <summary>True when the email was already actively subscribed, so the call created no new state.</summary>
    public bool AlreadySubscribed { get; set; }

    public string Message { get; set; } = string.Empty;
}
