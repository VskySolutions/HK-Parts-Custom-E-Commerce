using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.EmailTemplates;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Email;

/// <summary>
/// Default <see cref="IEmailTemplateSender"/>: loads the stored template by key, merges tenant branding
/// tokens with the caller's variables, renders the subject + HTML body with the shared token engine, and
/// enqueues the result as an HTML email. Disabled or missing templates are skipped (nothing is sent).
/// </summary>
public class EmailTemplateSender : IEmailTemplateSender
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailEnqueuer _emails;
    private readonly IUnsubscribeTokenService _unsubscribeTokens;
    private readonly string? _publicBaseUrl;

    public EmailTemplateSender(
        IApplicationDbContext db,
        IEmailEnqueuer emails,
        IUnsubscribeTokenService unsubscribeTokens,
        IConfiguration configuration)
    {
        _db = db;
        _emails = emails;
        _unsubscribeTokens = unsubscribeTokens;
        _publicBaseUrl = configuration["Storefront:PublicBaseUrl"];
    }

    public async Task<bool> SendAsync(
        string templateKey,
        string recipientEmail,
        string? recipientName,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            return false;

        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey, cancellationToken);
        if (template is null || !template.Enabled)
            return false;

        var branding = await _db.TenantBrandings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        // Branding tokens the shared HTML shell references; caller variables override/extend them.
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brandName"] = branding?.BrandName ?? string.Empty,
            ["primaryColor"] = string.IsNullOrWhiteSpace(branding?.PrimaryColor) ? "#111827" : branding!.PrimaryColor!,
            ["footerAddress"] = string.Empty,
            ["unsubscribeBlock"] = string.Empty,
        };
        foreach (var kv in variables)
            values[kv.Key] = kv.Value ?? string.Empty;

        // WO-87 (AC-ENT-006.1/006.2): inject a per-recipient unsubscribe link into Marketing emails. The
        // seeded HTML shell renders {{unsubscribeBlock}} in the footer; the dispatch worker adds the matching
        // link to the plain-text alternative. Set after the caller-variable merge so it can't be overwritten.
        if (template.Category == NotificationCategory.Marketing)
        {
            var unsubscribeUrl = MarketingEmailContent.BuildUnsubscribeUrl(
                _publicBaseUrl, _unsubscribeTokens.Generate(recipientEmail));
            values["unsubscribeBlock"] = MarketingEmailContent.BuildUnsubscribeHtmlBlock(unsubscribeUrl);
        }

        var subject = PreviewEmailTemplateQueryHandler.Render(template.SubjectLine, values);
        var body = PreviewEmailTemplateQueryHandler.Render(template.HtmlBody, values);

        await _emails.EnqueueAsync(
            templateKey,
            recipientEmail,
            recipientName,
            subject,
            body,
            template.Category,
            isHtml: true,
            cancellationToken: cancellationToken);

        return true;
    }
}
