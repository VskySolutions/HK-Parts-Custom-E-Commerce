using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A customer-submitted rating + written review of a <see cref="Product"/> (WO-14). Only verified
/// purchasers may submit; reviews start <see cref="ReviewStatus.Pending"/> and appear on the storefront
/// only once an admin approves them. <see cref="ProductId"/> and <see cref="CustomerId"/> are stored as
/// plain references; the reviewer's <see cref="ReviewerName"/> is snapshotted at submission so it
/// survives later profile edits.
/// </summary>
public class ProductReview : AuditableEntity, ISoftDeletable
{
    public Guid ProductId { get; set; }
    /// <summary>Optional navigation to the reviewed product (no inverse collection on Product).</summary>
    public Product? Product { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Star rating, 1–5.</summary>
    public int Rating { get; set; }

    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    /// <summary>Reviewer display name (Customer first + last), snapshotted at submission time.</summary>
    public string ReviewerName { get; set; } = string.Empty;

    /// <summary>When an admin last approved/rejected this review; null while still <see cref="ReviewStatus.Pending"/>.</summary>
    public DateTime? ModeratedOnUtc { get; set; }
    /// <summary>The admin <see cref="User"/> id that moderated this review; null while still Pending.</summary>
    public Guid? ModeratedById { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
