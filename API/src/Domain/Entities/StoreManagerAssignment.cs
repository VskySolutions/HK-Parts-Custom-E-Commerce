using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Links a Store Manager (a <see cref="User"/>) to the single <see cref="Store"/> whose order queue and
/// inventory they may manage (Store Management REQ-STR-004; scopes the store order queue in WO-52).
/// </summary>
public class StoreManagerAssignment : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
}
