using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Enqueues a pre-rendered email for asynchronous dispatch by the email worker (Email Dispatch
/// blueprint). Used for customer verification and password-reset messages (WO-20).
/// </summary>
public interface IEmailEnqueuer
{
    /// <summary>
    /// Queues an email. <paramref name="templateKey"/> records which template the message originated
    /// from (for auditing/suppression); <paramref name="subject"/>/<paramref name="body"/> are the
    /// already-rendered content.
    /// </summary>
    Task EnqueueAsync(
        string templateKey,
        string recipientEmail,
        string? recipientName,
        string subject,
        string body,
        NotificationCategory category = NotificationCategory.Transactional,
        bool isHtml = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues an email with optional CC / Reply-To and a related entity id. The From address is always the
    /// configured SMTP account. Implementations that predate CC support fall back to the plain overload.
    /// </summary>
    Task EnqueueAsync(EmailEnvelope envelope, CancellationToken cancellationToken = default) =>
        EnqueueAsync(envelope.TemplateKey, envelope.RecipientEmail, envelope.RecipientName, envelope.Subject,
            envelope.Body, envelope.Category, envelope.IsHtml, cancellationToken);
}

/// <summary>A rendered email plus the optional headers the plain overload cannot carry.</summary>
public sealed record EmailEnvelope(
    string TemplateKey,
    string RecipientEmail,
    string? RecipientName,
    string Subject,
    string Body,
    NotificationCategory Category = NotificationCategory.Transactional,
    bool IsHtml = true,
    string? CcEmail = null,
    string? ReplyToEmail = null,
    Guid? RelatedEntityId = null);
