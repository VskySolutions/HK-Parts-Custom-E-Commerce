using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// An editable email template keyed 1:1 to a notification event. Supports mustache-style
/// {{variable}} tokens and dual HTML/plain-text bodies (Email Notification Templates blueprint).
/// </summary>
public class EmailTemplate : AuditableEntity
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubjectLine { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public NotificationCategory Category { get; set; } = NotificationCategory.Transactional;
    public bool Enabled { get; set; } = true;
    public bool IsCritical { get; set; }
    public string? Description { get; set; }
}
