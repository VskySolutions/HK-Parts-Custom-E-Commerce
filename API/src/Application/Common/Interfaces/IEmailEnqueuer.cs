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
}
