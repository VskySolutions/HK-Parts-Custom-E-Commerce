using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>Execution record for a scheduled background task (Background Task Scheduler blueprint).</summary>
public class BackgroundTaskLog : BaseEntity
{
    public string TaskName { get; set; } = string.Empty;
    public DateTime StartedOnUtc { get; set; }
    public DateTime? CompletedOnUtc { get; set; }
    public string Status { get; set; } = "Running"; // Running | Succeeded | Failed
    public string? ErrorMessage { get; set; }
}
