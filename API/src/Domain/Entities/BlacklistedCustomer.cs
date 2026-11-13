using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A customer an admin has blocked (Customers list → Blacklist). This table is the source of truth for who is
/// blacklisted: the <see cref="Customer"/> record and everything hanging off it (orders, addresses, wishlist,
/// documents...) is left untouched. At most one active (not soft-deleted) record per customer, enforced by a
/// filtered unique index; soft delete keeps the history so a block can be reversed later.
/// <para>Blocked by = <see cref="AuditableEntity.CreatedById"/>.</para>
/// </summary>
public class BlacklistedCustomer : AuditableEntity, ISoftDeletable
{
    public const int ReasonMaxLength = 1000;
    public const int InternalNotesMaxLength = 2000;

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Required "Reason for Blocking".</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional notes visible to admins only.</summary>
    public string? InternalNotes { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
