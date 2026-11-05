using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A queued, pre-rendered email awaiting asynchronous dispatch. Processed by the email dispatch
/// worker with retry/backoff and suppression checks (Email Dispatch blueprint).
/// </summary>
public class EmailQueue : BaseEntity
{
    public string TemplateKey { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string RenderedSubject { get; set; } = string.Empty;
    public string RenderedBody { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }

    /// <summary>True when <see cref="RenderedBody"/> is HTML (rendered from an email template); false = plain text.</summary>
    public bool IsHtml { get; set; }

    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptOnUtc { get; set; }
    public DateTime? NextAttemptOnUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>Optional CC (e.g. the gift-certificate sender).</summary>
    public string? CcEmail { get; set; }
    /// <summary>Optional Reply-To. The From address is always the configured SMTP account, never a user.</summary>
    public string? ReplyToEmail { get; set; }
    /// <summary>The entity the email is about (e.g. a gift certificate), for post-send bookkeeping.</summary>
    public Guid? RelatedEntityId { get; set; }
}
