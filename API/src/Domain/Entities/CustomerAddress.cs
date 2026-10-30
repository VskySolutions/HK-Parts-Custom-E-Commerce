using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A customer's address-book entry: maps a <see cref="Customer"/> to a shared <see cref="Address"/> row
/// with its book role (<see cref="AddressType"/>) and default flag. At most one default per
/// (customer, type) — enforced by a filtered unique index (REQ-CUS-002).
/// </summary>
public class CustomerAddress : AuditableEntity, ISoftDeletable
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid AddressId { get; set; }
    public Address? Address { get; set; }

    public AddressType AddressType { get; set; }
    public bool IsDefault { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
