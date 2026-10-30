using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A recipient who has unsubscribed from marketing email (Email Dispatch blueprint).</summary>
public class MarketingSuppression : BaseEntity
{
    public string RecipientEmail { get; set; } = string.Empty;
    public DateTime SuppressedOnUtc { get; set; }
    public string Source { get; set; } = string.Empty; // e.g. "unsubscribe-link", "admin"
}
