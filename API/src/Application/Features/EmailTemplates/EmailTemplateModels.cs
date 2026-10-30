using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

/// <summary>Compact list view of an email template.</summary>
public class EmailTemplateSummaryDto
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }
    public bool Enabled { get; set; }
    public bool IsCritical { get; set; }

    /// <summary>DisplayName of the enabled SMTP account serving this template's category, or null when none is configured.</summary>
    public string? AssignedSmtpAccountName { get; set; }

    /// <summary>False signals the "no SMTP account configured" warning for this template's category.</summary>
    public bool HasSmtpConfigured { get; set; }

    public static EmailTemplateSummaryDto From(EmailTemplate t) => new()
    {
        TemplateKey = t.TemplateKey,
        Name = t.Name,
        Category = t.Category,
        Enabled = t.Enabled,
        IsCritical = t.IsCritical,
    };
}

/// <summary>Full editable view of an email template, including subject and both bodies.</summary>
public class EmailTemplateDto
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubjectLine { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public NotificationCategory Category { get; set; }
    public bool Enabled { get; set; }
    public bool IsCritical { get; set; }
    public string? Description { get; set; }

    public static EmailTemplateDto From(EmailTemplate t) => new()
    {
        TemplateKey = t.TemplateKey,
        Name = t.Name,
        SubjectLine = t.SubjectLine,
        HtmlBody = t.HtmlBody,
        PlainTextBody = t.PlainTextBody,
        Category = t.Category,
        Enabled = t.Enabled,
        IsCritical = t.IsCritical,
        Description = t.Description,
    };
}

/// <summary>Rendered preview of a template using sample token values.</summary>
public class EmailTemplatePreviewDto
{
    public string Subject { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
}
