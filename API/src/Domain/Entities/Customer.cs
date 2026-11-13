using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Profile data linked 1:1 to a <see cref="User"/>. Holds all personal and preference data, kept
/// separate from authentication credentials (Database blueprint).
/// </summary>
public class Customer : AuditableEntity, ISoftDeletable
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    /// <summary>Optional IANA timezone id chosen by the customer; overrides the tenant display zone for
    /// their own storefront views. Null = follow the tenant default.</summary>
    public string? PreferredTimeZone { get; set; }

    /// <summary>
    /// The customer's pricing group (AC-CUS-003.2) — at most one; null = base pricing. Assigning a new
    /// group simply replaces this value. Independent of the <see cref="RoleType.Customer"/> RBAC role
    /// that every registered shopper carries.
    /// </summary>
    public Guid? CustomerGroupId { get; set; }
    public CustomerGroup? CustomerGroup { get; set; }

    /// <summary>
    /// Current blacklist state (Customers list → Blacklist). Only the blacklist service methods may change it;
    /// <see cref="BlacklistedCustomer"/> rows are the append-only event history. Blacklisted customers are hidden
    /// from the admin Customers list but their data is untouched.
    /// </summary>
    public bool IsBlacklisted { get; set; }

    public bool IsTaxExempt { get; set; }
    public string? TaxExemptionCertificate { get; set; }
    public string? VatId { get; set; }
    public string? WhatsAppPhoneNumber { get; set; }
    public bool WhatsAppOptIn { get; set; }
    public DateTime? WhatsAppOptInAtUtc { get; set; }

    // ---- Custom information (admin-maintained) ----

    /// <summary>"Long" free-text custom field (admin notes / long text). Null when not set.</summary>
    public string? LongText { get; set; }

    /// <summary>
    /// Whether this customer may buy restricted-capacity items — the customer-side counterpart of the
    /// product attribute "Restrict Non High Capacity Mags States" (options No / Yes). Defaults to No.
    /// </summary>
    public bool AllowRestrictedCapacityPurchases { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public User? User { get; set; }

    /// <summary>Admin-uploaded documents (active and soft-deleted).</summary>
    public ICollection<CustomerDocument> Documents { get; set; } = new List<CustomerDocument>();
}
