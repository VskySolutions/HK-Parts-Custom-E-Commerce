using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.MarketingSuppression;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public marketing-email unsubscribe endpoint (WO-87). The leading-slash action route is absolute, escaping
/// the <see cref="ApiControllerBase"/> <c>api/[controller]</c> prefix so the link sits at the site root where
/// email footers point. No authentication required (AC-ENT-006.2): the signed token is the sole credential.
/// </summary>
[AllowAnonymous]
public class UnsubscribeController : ApiControllerBase
{
    /// <summary>Records the recipient on the Marketing Suppression List and returns an HTML confirmation
    /// page. An invalid/expired token yields a friendly error page (HTTP 400), never a stack trace.</summary>
    [HttpGet("/unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromQuery] string? token)
    {
        var result = await Mediator.Send(new UnsubscribeCommand(token));
        return result.Succeeded
            ? Content(SuccessPage(result.Email), "text/html")
            : new ContentResult
            {
                Content = ErrorPage(),
                ContentType = "text/html",
                StatusCode = StatusCodes.Status400BadRequest,
            };
    }

    private static string SuccessPage(string email) => Page(
        "You've been unsubscribed",
        $"<p>{WebUtility.HtmlEncode(email)} has been removed from our marketing emails.</p>" +
        "<p>You will no longer receive promotional messages from us. Transactional emails about your orders will still be sent.</p>");

    private static string ErrorPage() => Page(
        "Link no longer valid",
        "<p>This unsubscribe link is invalid or has expired.</p>" +
        "<p>If you continue to receive unwanted marketing emails, please use the unsubscribe link in a more recent message.</p>");

    // Minimal self-contained confirmation/error shell — no external assets, safe to render anonymously.
    private static string Page(string heading, string bodyHtml) =>
        $$"""
        <!DOCTYPE html>
        <html lang="en">
          <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <meta name="robots" content="noindex">
            <title>{{heading}}</title>
          </head>
          <body style="margin:0;padding:0;background:#f4f5f7;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:48px 0;">
              <tr><td align="center">
                <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;max-width:90%;">
                  <tr><td style="padding:40px 32px;">
                    <h1 style="margin:0 0 16px;font-size:22px;">{{heading}}</h1>
                    <div style="font-size:15px;line-height:1.6;color:#4b5563;">{{bodyHtml}}</div>
                  </td></tr>
                </table>
              </td></tr>
            </table>
          </body>
        </html>
        """;
}
