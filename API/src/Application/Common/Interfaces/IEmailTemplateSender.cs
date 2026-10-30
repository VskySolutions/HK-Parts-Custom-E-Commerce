namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Renders a stored, admin-editable email template (by <c>TemplateKey</c>) with the given variables
/// (plus tenant branding tokens) and enqueues it for delivery. This is how ALL transactional/marketing
/// notifications should be sent, so template edits in the admin UI take effect for real emails.
/// </summary>
public interface IEmailTemplateSender
{
    /// <summary>
    /// Renders the <paramref name="templateKey"/> template and queues it to <paramref name="recipientEmail"/>.
    /// Returns false (and sends nothing) when the recipient is blank or the template is missing/disabled.
    /// </summary>
    Task<bool> SendAsync(
        string templateKey,
        string recipientEmail,
        string? recipientName,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default);
}
