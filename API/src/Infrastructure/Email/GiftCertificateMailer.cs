using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.EmailTemplates;
using HKPartsEcommerce.Application.Features.GiftCertificates;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Email;

/// <summary>The gift-certificate email template (seeded; admin-editable under Email Templates).</summary>
public static class GiftCertificateEmailTemplate
{
    public const string Key = "gift-certificate.issued";
    public const string Subject = "{{senderName}} sent you a {{storeName}} gift certificate";
    public const string Heading = "You've received a gift certificate";
    public const string Body =
        "{{logoBlock}}" +
        "<p>Hello {{recipientName}},</p>" +
        "<p>{{senderName}} has sent you a gift certificate for <strong>{{amount}}</strong>.</p>" +
        "{{messageBlock}}" +
        "<div style=\"margin:24px 0;padding:18px;border:2px dashed {{primaryColor}};border-radius:8px;text-align:center\">" +
        "<div style=\"font-size:12px;letter-spacing:1px;text-transform:uppercase;color:#6b7280\">Your gift certificate code</div>" +
        "<div style=\"font-size:26px;font-weight:700;letter-spacing:3px;font-family:monospace;margin-top:6px\">{{code}}</div>" +
        "</div>" +
        "<p>Use it at checkout on <a href=\"{{storeUrl}}\">{{storeUrl}}</a>.</p>" +
        "{{expiryBlock}}";
}

/// <summary>
/// Renders and queues the gift-certificate email: To the recipient, CC + Reply-To the sender, From the
/// store's configured SMTP address (never the sender's own address). The dispatch worker retries delivery and
/// stamps <c>EmailSentAtUtc</c> once it is actually sent.
/// </summary>
public sealed class GiftCertificateMailer : IGiftCertificateMailer
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailEnqueuer _emails;
    private readonly string? _storeUrl;

    public GiftCertificateMailer(IApplicationDbContext db, IEmailEnqueuer emails, IConfiguration configuration)
    {
        _db = db;
        _emails = emails;
        _storeUrl = configuration["Storefront:PublicBaseUrl"];
    }

    public async Task<bool> QueueAsync(Guid giftCertificateId, CancellationToken ct = default)
    {
        var gc = await _db.GiftCertificates.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == giftCertificateId, ct);
        if (gc is null || string.IsNullOrWhiteSpace(gc.RecipientEmail))
            return false;

        var branding = await _db.TenantBrandings.AsNoTracking().Include(b => b.LogoMedia).FirstOrDefaultAsync(ct);
        var storeName = branding?.BrandName is { Length: > 0 } brand
            ? brand
            : await _db.Stores.AsNoTracking().Where(s => s.Id == gc.StoreId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "our store";
        var logoUrl = branding?.LogoMedia?.Url ?? branding?.LogoUrl;
        var storeUrl = (_storeUrl ?? string.Empty).TrimEnd('/');
        var sender = string.IsNullOrWhiteSpace(gc.SenderName) ? storeName : gc.SenderName!;
        var recipient = string.IsNullOrWhiteSpace(gc.RecipientName) ? "there" : gc.RecipientName!;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brandName"] = branding?.BrandName ?? storeName,
            ["primaryColor"] = string.IsNullOrWhiteSpace(branding?.PrimaryColor) ? "#111827" : branding!.PrimaryColor!,
            ["footerAddress"] = string.Empty,
            ["unsubscribeBlock"] = string.Empty,
            ["storeName"] = Html(storeName),
            ["senderName"] = Html(sender),
            ["recipientName"] = Html(recipient),
            ["amount"] = gc.OriginalBalance.ToString("C", CultureInfo.GetCultureInfo("en-US")),
            ["balance"] = gc.CurrentBalance.ToString("C", CultureInfo.GetCultureInfo("en-US")),
            ["code"] = Html(gc.Code),
            ["storeUrl"] = Html(storeUrl),
            ["logoBlock"] = string.IsNullOrWhiteSpace(logoUrl)
                ? string.Empty
                : $"<p style=\"text-align:center\"><img src=\"{Html(AbsoluteUrl(logoUrl!, storeUrl))}\" alt=\"{Html(storeName)}\" style=\"max-height:64px\"></p>",
            ["messageBlock"] = string.IsNullOrWhiteSpace(gc.Message)
                ? string.Empty
                : $"<blockquote style=\"margin:16px 0;padding:12px 16px;border-left:4px solid {{{{primaryColor}}}};background:#f9fafb;white-space:pre-line\">{Html(gc.Message!)}</blockquote>",
            ["expiryBlock"] = gc.ExpiresAtUtc is DateTime expires
                ? $"<p style=\"color:#6b7280\">This gift certificate expires on {expires:MMMM d, yyyy}.</p>"
                : string.Empty,
        };

        var template = await _db.EmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == GiftCertificateEmailTemplate.Key, ct);
        var subjectTemplate = template?.Enabled == true ? template.SubjectLine : GiftCertificateEmailTemplate.Subject;
        var bodyTemplate = template?.Enabled == true ? template.HtmlBody : GiftCertificateEmailTemplate.Body;

        // Two passes so the {{primaryColor}} inside the message block resolves too.
        var body = PreviewEmailTemplateQueryHandler.Render(PreviewEmailTemplateQueryHandler.Render(bodyTemplate, values), values);
        var subject = WebUtility.HtmlDecode(PreviewEmailTemplateQueryHandler.Render(subjectTemplate, values));

        await _emails.EnqueueAsync(new EmailEnvelope(
            GiftCertificateEmailTemplate.Key, gc.RecipientEmail!, gc.RecipientName, subject, body,
            NotificationCategory.Transactional, IsHtml: true,
            CcEmail: gc.SenderEmail, ReplyToEmail: gc.SenderEmail, RelatedEntityId: gc.Id), ct);
        return true;
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string AbsoluteUrl(string url, string origin) =>
        Uri.TryCreate(url, UriKind.Absolute, out _) || string.IsNullOrEmpty(origin) ? url : origin + "/" + url.TrimStart('/');
}
