using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Low-stock checkout lock (CK-20..23): while a cart holds the lock on a product, no other cart may check
/// that product out. One row per product (unique). A lock keeps its original deadline: the owning cart
/// refreshing the checkout never extends it (CK-21). Released when the owner places its order, or by the
/// storefront countdown, or simply by expiring.
/// </summary>
public class CheckoutLock : BaseEntity
{
    public Guid ProductId { get; set; }

    /// <summary>The cart that holds the lock.</summary>
    public Guid OwnerCartId { get; set; }

    public DateTime LockedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

/// <summary>
/// A blocked buyer (PY-10): a placement whose billing name and / or email matches an active entry is refused
/// before any gateway is contacted, whatever the payment path (card, gift certificate, zero total).
/// How an entry matches is the checkout setting <c>Checkout.BlacklistMatchMode</c> (PY-15).
/// </summary>
public class CustomerBlacklistEntry : AuditableEntity
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Reason { get; set; }
    public CustomerBlacklistStatus Status { get; set; } = CustomerBlacklistStatus.Blocked;

    public ICollection<CustomerBlacklistHit> Hits { get; set; } = new List<CustomerBlacklistHit>();
}

/// <summary>One refused placement attempt (PY-15 "log each block").</summary>
public class CustomerBlacklistHit : BaseEntity
{
    public Guid BlacklistEntryId { get; set; }
    public CustomerBlacklistEntry? BlacklistEntry { get; set; }

    public Guid? CartId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? IpAddress { get; set; }

    /// <summary>What matched: "Email", "Name" or "Name+Email".</summary>
    public string MatchedOn { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
}
