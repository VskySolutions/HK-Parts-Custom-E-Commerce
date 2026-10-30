using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

/// <summary>
/// Renders a full-fidelity preview of an email template with system-provided sample data
/// (WO-79, REQ-ENT-003). Unsaved edits can be previewed by supplying <paramref name="Subject"/>,
/// <paramref name="HtmlBody"/>, and/or <paramref name="PlainTextBody"/> overrides; any omitted (null)
/// field falls back to the stored template, so the admin can preview edits without saving first
/// (AC-ENT-003.1). Read-only — no side effects.
/// </summary>
public record PreviewEmailTemplateContentQuery(
    string Key,
    string? Subject = null,
    string? HtmlBody = null,
    string? PlainTextBody = null) : IRequest<EmailTemplatePreviewResult>;

/// <summary>
/// Rendered preview exposing the subject, sender name, and both HTML and plain-text bodies so the UI
/// can display and visually distinguish the two renderings (AC-ENT-003.3 / AC-ENT-003.4).
/// </summary>
public record EmailTemplatePreviewResult(
    string Subject,
    string FromName,
    string HtmlBody,
    string TextBody);

public class PreviewEmailTemplateContentQueryHandler
    : IRequestHandler<PreviewEmailTemplateContentQuery, EmailTemplatePreviewResult>
{
    private readonly IApplicationDbContext _db;

    public PreviewEmailTemplateContentQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<EmailTemplatePreviewResult> Handle(
        PreviewEmailTemplateContentQuery request, CancellationToken cancellationToken)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == request.Key, cancellationToken)
            ?? throw new NotFoundException("EmailTemplate", request.Key);

        // Prefer caller-supplied (unsaved) content per field, else the stored template (AC-ENT-003.1).
        var subject = request.Subject ?? template.SubjectLine;
        var html = request.HtmlBody ?? template.HtmlBody;
        var text = request.PlainTextBody ?? template.PlainTextBody ?? string.Empty;

        // Substitute every dynamic variable with sample values for this event (AC-ENT-003.2). Reuses the
        // existing token-substitution utility so preview semantics stay consistent across endpoints.
        var values = TemplatePreviewData.ForTemplate(template.TemplateKey, template.Category);
        string Render(string s) => PreviewEmailTemplateQueryHandler.Render(s, values);

        // From-name comes from the SMTP account assigned to the category; fall back to the sample brand.
        var account = await EmailTemplateSmtpResolver.ResolveForCategoryAsync(_db, template.Category, cancellationToken);
        var fromName = account?.FromName is { Length: > 0 } name
            ? name
            : values.GetValueOrDefault("brandName", string.Empty);

        return new EmailTemplatePreviewResult(
            Subject: Render(subject),
            FromName: fromName,
            HtmlBody: Render(html),
            TextBody: Render(text));
    }
}
