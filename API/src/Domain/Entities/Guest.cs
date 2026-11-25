using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A buyer who checked out without an account (docs/guest-checkout-requirements.md §5.2). One row per
/// normalized (trimmed, lower-case) email, so admins can act on a guest by an opaque id instead of putting
/// the email in a URL (A-15, D5). Name and phone come from the guest's latest order. Guest orders point
/// here through <see cref="Order.GuestId"/>; the row stays after the orders are linked to an account.
/// </summary>
public class Guest : AuditableEntity
{
    /// <summary>Trimmed, lower-case email (unique). See <see cref="NormalizeEmail"/>.</summary>
    public string EmailNormalized { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }

    /// <summary>U-14: the guest ticked "Email me news and offers" (unchecked by default).</summary>
    public bool MarketingOptIn { get; set; }
    public DateTime? OptInOnUtc { get; set; }

    /// <summary>Set when the guest's orders were linked to a customer account (U-71, A-18).</summary>
    public Guid? LinkedCustomerId { get; set; }
    public Customer? LinkedCustomer { get; set; }
    public DateTime? LinkedOnUtc { get; set; }

    public DateTime? LastOrderOnUtc { get; set; }

    /// <summary>N-05: when this guest's personal data was anonymised on request; null = never.</summary>
    public DateTime? AnonymizedOnUtc { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    /// <summary>The single normalization rule for guest emails (A-12, D6): trimmed and lower-case.</summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>
/// One guest order lookup attempt (U-60/U-62): the rate limit counts failures per IP, and the rows double as
/// an audit trail. No email or order number is stored (N-05): only the order matched, if any.
/// </summary>
public class GuestOrderAccessLog : BaseEntity
{
    public Guid? OrderId { get; set; }
    public string? IpAddress { get; set; }
    public bool Success { get; set; }

    /// <summary>"Lookup" (order number + email/ZIP form) or "Link" (signed email link).</summary>
    public string Method { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
}
