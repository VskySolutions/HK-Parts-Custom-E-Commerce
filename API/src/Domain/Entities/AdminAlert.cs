using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// An operational alert surfaced to admins — e.g. the active SMTP account for a category failing
/// delivery (WO-75 AC-TEN-003.5), or storage unavailability. Raised by the relevant feature.
/// </summary>
public class AdminAlert : BaseEntity
{
    public string AlertType { get; set; } = string.Empty;   // e.g. "SmtpDeliveryFailure"
    public string Severity { get; set; } = "Warning";       // Info | Warning | Error
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Source { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ResolvedOnUtc { get; set; }
}
