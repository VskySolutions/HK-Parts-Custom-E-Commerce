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
    private readonly IGuestOrderTokenService? _guestTokens;
    private readonly ISettingsService? _settings;
    private readonly IStorefrontUrlBuilder? _urls;

    public EmailTemplateSender(
        IApplicationDbContext db,
        IEmailEnqueuer emails,
        IUnsubscribeTokenService unsubscribeTokens,
        IConfiguration configuration,
        IGuestOrderTokenService? guestTokens = null,
        ISettingsService? settings = null,
        IStorefrontUrlBuilder? urls = null)
    {
        _db = db;
        _emails = emails;
        _unsubscribeTokens = unsubscribeTokens;
        _publicBaseUrl = configuration["Storefront:PublicBaseUrl"];
        _guestTokens = guestTokens;
        _settings = settings;
        _urls = urls;
    }

    /// <summary>Template tokens for the guest "View your order" link (guest checkout U-52).</summary>
    public const string OrderViewUrlToken = "orderViewUrl";
    public const string OrderViewBlockToken = "orderViewBlock";

    /// <summary>
    /// Guest checkout U-52: an email about a guest order, sent to that order's own email, carries a signed
    /// "View your order" link. Sets <c>{{orderViewUrl}}</c> / <c>{{orderViewBlock}}</c>; a template that places
    /// neither (every template saved before this existed) gets the block put in front of its footer links, so
    /// admin-edited templates need no change. Store notifications go to staff, so the recipient check skips them.
    /// </summary>
    private async Task<string> ApplyGuestOrderLinkAsync(
        string html, string recipientEmail, Dictionary<string, string> values, CancellationToken ct)
    {
        values.TryAdd(OrderViewUrlToken, string.Empty);
        values.TryAdd(OrderViewBlockToken, string.Empty);
        if (_guestTokens is null || _urls is null
            || values[OrderViewUrlToken].Length > 0
            || !values.TryGetValue("orderNumber", out var orderNumber) || string.IsNullOrWhiteSpace(orderNumber))
            return html;

        var recipient = Domain.Entities.Guest.NormalizeEmail(recipientEmail);
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderNumber == orderNumber && o.CustomerId == null && o.EbayOrderId == null && !o.IsInquiry)
            .Select(o => new { o.Id, Email = o.ShippingAddress != null ? o.ShippingAddress.Email : null })
            .FirstOrDefaultAsync(ct);
        if (order is null || Domain.Entities.Guest.NormalizeEmail(order.Email) != recipient)
            return html;

        var guest = await Application.Features.Checkout.GuestCheckoutSettingsStore.LoadAsync(_settings, ct);
        var url = _urls.GuestOrderUrl(_guestTokens.Generate(order.Id, TimeSpan.FromDays(guest.LinkLifetimeDays)));
        var safeUrl = System.Net.WebUtility.HtmlEncode(url);
        values[OrderViewUrlToken] = url;
        values[OrderViewBlockToken] =
            $"<p style=\"margin:0 0 12px;\"><a href=\"{safeUrl}\" style=\"display:inline-block;padding:10px 18px;" +
            "background:#111827;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:bold;\">View your order</a></p>" +
            $"<p style=\"margin:0 0 12px;\">No account needed. You can also look up your order at " +
            $"<a href=\"{System.Net.WebUtility.HtmlEncode(_urls.GuestOrderLookupUrl())}\">our order lookup page</a> with your order number and email.</p>";

        if (html.Contains("{{" + OrderViewBlockToken + "}}", StringComparison.OrdinalIgnoreCase)
            || html.Contains("{{" + OrderViewUrlToken + "}}", StringComparison.OrdinalIgnoreCase))
            return html;
        const string footerToken = "{{unsubscribeBlock}}";
        var at = html.IndexOf(footerToken, StringComparison.OrdinalIgnoreCase);
        if (at >= 0)
            return html.Insert(at, "{{" + OrderViewBlockToken + "}}");
        var bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd >= 0
            ? html.Insert(bodyEnd, "{{" + OrderViewBlockToken + "}}")
            : html + "{{" + OrderViewBlockToken + "}}";
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

        var html = template.Category == NotificationCategory.Transactional
            ? await ApplyGuestOrderLinkAsync(template.HtmlBody, recipientEmail, values, cancellationToken)
            : template.HtmlBody;

        var subject = PreviewEmailTemplateQueryHandler.Render(template.SubjectLine, values);
        var body = PreviewEmailTemplateQueryHandler.Render(html, values);

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
