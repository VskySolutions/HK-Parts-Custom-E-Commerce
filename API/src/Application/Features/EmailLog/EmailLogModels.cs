using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.EmailLog;

/// <summary>A row in the admin email send-log list (body omitted — see <see cref="EmailLogDetailDto"/>).</summary>
public class EmailLogItemDto
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }
    public EmailStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptOnUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public bool HasError { get; set; }

    public static EmailLogItemDto From(EmailQueue e) => new()
    {
        Id = e.Id,
        TemplateKey = e.TemplateKey,
        RecipientEmail = e.RecipientEmail,
        RecipientName = e.RecipientName,
        Subject = e.RenderedSubject,
        Category = e.Category,
        Status = e.Status,
        AttemptCount = e.AttemptCount,
        LastAttemptOnUtc = e.LastAttemptOnUtc,
        CreatedOnUtc = e.CreatedOnUtc,
        HasError = !string.IsNullOrEmpty(e.ErrorMessage),
    };
}

/// <summary>The full email record, including the rendered body and the last error.</summary>
public class EmailLogDetailDto
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }
    public EmailStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptOnUtc { get; set; }
    public DateTime? NextAttemptOnUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static EmailLogDetailDto From(EmailQueue e) => new()
    {
        Id = e.Id,
        TemplateKey = e.TemplateKey,
        RecipientEmail = e.RecipientEmail,
        RecipientName = e.RecipientName,
        Subject = e.RenderedSubject,
        Body = e.RenderedBody,
        Category = e.Category,
        Status = e.Status,
        AttemptCount = e.AttemptCount,
        LastAttemptOnUtc = e.LastAttemptOnUtc,
        NextAttemptOnUtc = e.NextAttemptOnUtc,
        ErrorMessage = e.ErrorMessage,
        CreatedOnUtc = e.CreatedOnUtc,
    };
}
