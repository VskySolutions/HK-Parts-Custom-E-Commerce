using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Append-only history of blacklist events (Customers list → Blacklist). Each row is one block or unblock, never
/// the current state: the current state is <see cref="Customer.IsBlacklisted"/>. The <see cref="Customer"/> record
/// and everything hanging off it (orders, addresses, wishlist, documents...) is left untouched.
/// <para>Performed by = <see cref="AuditableEntity.CreatedById"/>; the row is never updated or deleted.</para>
/// </summary>
public class BlacklistedCustomer : AuditableEntity
{
    public const int ReasonMaxLength = 1000;
    public const int InternalNotesMaxLength = 2000;

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Whether this event blocked (<see cref="BlacklistAction.Blocked"/>) or unblocked the customer.</summary>
    public BlacklistAction Action { get; set; }

    /// <summary>Required "Reason for Blocking" on <see cref="BlacklistAction.Blocked"/> rows; null on unblocks.</summary>
    public string? Reason { get; set; }

    /// <summary>Optional notes visible to admins only.</summary>
    public string? InternalNotes { get; set; }
}
