using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Logging;

/// <summary>
/// Summary projection of an <see cref="ApplicationLog"/> for the admin Log Viewer list (WO-70).
/// Deliberately omits the raw <c>Exception</c> stack trace and <c>Properties</c> payload — the list
/// surfaces only a trimmed message plus correlation metadata (AC: "summary and CorrelationId only —
/// no stack traces in API response").
/// </summary>
public class ApplicationLogDto
{
    /// <summary>Maximum number of characters of the message surfaced in the list projection.</summary>
    private const int MessageSummaryMaxLength = 500;

    public long Id { get; set; }
    public string Level { get; set; } = string.Empty;

    /// <summary>Trimmed, summary-only message (never a stack trace).</summary>
    public string? Message { get; set; }
    public DateTime TimeStamp { get; set; }
    public string? CorrelationId { get; set; }
    public string? Source { get; set; }
    public string? Route { get; set; }

    public static ApplicationLogDto From(ApplicationLog log) => new()
    {
        Id = log.Id,
        Level = log.Level,
        Message = Summarize(log.Message),
        TimeStamp = log.TimeStamp,
        CorrelationId = log.CorrelationId,
        Source = log.Source,
        Route = log.Route,
    };

    private static string? Summarize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var trimmed = message.Trim();
        return trimmed.Length <= MessageSummaryMaxLength
            ? trimmed
            : trimmed[..MessageSummaryMaxLength] + "...";
    }
}
