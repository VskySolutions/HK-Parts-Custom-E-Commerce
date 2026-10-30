using System.Net;
using System.Text.RegularExpressions;

namespace HKPartsEcommerce.Infrastructure.Email;

/// <summary>
/// Builds the unsubscribe artifacts shared by the marketing-email send path (WO-87): the absolute public
/// unsubscribe URL, the HTML footer snippet substituted into the shared shell's <c>{{unsubscribeBlock}}</c>
/// slot (used by <see cref="EmailTemplateSender"/>), and a plain-text body that always carries the link
/// (materialised by the dispatch worker so the text/plain alternative satisfies AC-ENT-006.1). Centralised so
/// the HTML and text renderings stay identical in format.
/// </summary>
public static class MarketingEmailContent
{
    // Matches StorefrontUrlBuilder's dev fallback when no public base URL is configured.
    private const string DefaultBaseUrl = "http://localhost:9000";

    /// <summary>
    /// The absolute unsubscribe URL: <c>{base}/unsubscribe?token={token}</c>. The token is URL-encoded
    /// (mirroring StorefrontUrlBuilder) so any '+' or '/' in the payload cannot corrupt the query string.
    /// </summary>
    public static string BuildUnsubscribeUrl(string? baseUrl, string token)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
        return $"{root}/unsubscribe?token={Uri.EscapeDataString(token)}";
    }

    /// <summary>The muted HTML footer snippet placed into the shared shell's <c>{{unsubscribeBlock}}</c> slot.</summary>
    public static string BuildUnsubscribeHtmlBlock(string unsubscribeUrl) =>
        $"<p style=\"margin:8px 0 0;\"><a href=\"{WebUtility.HtmlEncode(unsubscribeUrl)}\" style=\"color:#6b7280;\">Unsubscribe</a> from these emails.</p>";

    /// <summary>
    /// A plain-text rendition of the rendered HTML body with the unsubscribe link appended, so the text/plain
    /// alternative always contains a working link (AC-ENT-006.1). Tag-strips the HTML the same way the seeded
    /// templates derive their plain-text bodies (DefaultEmailTemplates.ToPlainText); the link is appended
    /// explicitly because the href in the stripped anchor would otherwise be lost.
    /// </summary>
    public static string BuildTextAlternative(string html, string unsubscribeUrl)
    {
        var text = Regex.Replace(html ?? string.Empty, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "\\s+", " ").Trim();
        return $"{text}\n\nTo unsubscribe from these emails, visit: {unsubscribeUrl}";
    }
}
