using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A registered customer's wishlist (REQ-CHK-002). One active wishlist per customer; adding items never
/// reserves stock or affects inventory (AC-CHK-002.3).
/// </summary>
public class Wishlist : AuditableEntity, ISoftDeletable
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<WishlistItem> Items { get; set; } = new List<WishlistItem>();
}

/// <summary>A saved product (optionally a specific variant) in a <see cref="Wishlist"/> (AC-CHK-002.1).</summary>
public class WishlistItem : AuditableEntity
{
    public Guid WishlistId { get; set; }
    public Wishlist? Wishlist { get; set; }

    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
}
