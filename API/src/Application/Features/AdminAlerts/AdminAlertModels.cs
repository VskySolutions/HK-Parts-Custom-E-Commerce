using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.AdminAlerts;

/// <summary>An operational alert surfaced to admins for triage and resolution.</summary>
public class AdminAlertDto
{
    public Guid Id { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Source { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ResolvedOnUtc { get; set; }

    public static AdminAlertDto From(AdminAlert a) => new()
    {
        Id = a.Id,
        AlertType = a.AlertType,
        Severity = a.Severity,
        Title = a.Title,
        Message = a.Message,
        Source = a.Source,
        IsResolved = a.IsResolved,
        CreatedOnUtc = a.CreatedOnUtc,
        ResolvedOnUtc = a.ResolvedOnUtc,
    };
}
