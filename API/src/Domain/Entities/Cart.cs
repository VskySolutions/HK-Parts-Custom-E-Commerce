using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A shopping cart owned by an authenticated customer or an anonymous session (REQ-CHK-001).
/// Persisted so a registered buyer's cart is restored on return (AC-CHK-001.4).
/// </summary>
public class Cart : AuditableEntity, ISoftDeletable
{
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Anonymous session identifier for guest carts (null once associated with a customer).</summary>
    public string? SessionId { get; set; }

    public string? AppliedCouponCode { get; set; }

    /// <summary>Loyalty points staged for redemption at checkout, and the discount they buy (WO-27). Spent
    /// only at order placement — like a coupon, applying does not deduct the balance.</summary>
    public int PointsApplied { get; set; }
    public decimal PointsDiscountAmount { get; set; }

    public string CurrencyCode { get; set; } = "USD";
    public bool IsCheckedOut { get; set; }

    /// <summary>When a recovery email was sent for this abandoned cart; null = not yet notified (WO-31).</summary>
    public DateTime? AbandonmentNotifiedOnUtc { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

/// <summary>A line in a <see cref="Cart"/> with the unit price snapshotted when added (AC-CHK-001.1).</summary>
public class CartItem : AuditableEntity
{
    public Guid CartId { get; set; }
    public Cart? Cart { get; set; }

    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// The values the buyer typed into the product's CustomInput attributes, as a JSON array of
    /// { attributeId, name, value } (null when the product has none). Two lines for the same
    /// product/variant only merge when these match, so a second engraving is its own line.
    /// Read and written through <c>CustomAttributes</c> in Application.Common.Models.
    /// </summary>
    public string? CustomAttributesJson { get; set; }
}
