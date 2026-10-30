namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>Last-run and next-run status for a scheduled background task.</summary>
public class BackgroundTaskStatus
{
    public string TaskName { get; set; } = string.Empty;
    public DateTime? LastStartedUtc { get; set; }
    public DateTime? LastCompletedUtc { get; set; }
    public string? LastStatus { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRunUtc { get; set; }
}
