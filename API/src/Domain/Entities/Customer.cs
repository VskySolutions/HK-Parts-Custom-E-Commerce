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

    public bool IsTaxExempt { get; set; }
    public string? TaxExemptionCertificate { get; set; }
    public string? VatId { get; set; }
    public string? WhatsAppPhoneNumber { get; set; }
    public bool WhatsAppOptIn { get; set; }
    public DateTime? WhatsAppOptInAtUtc { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public User? User { get; set; }
}
