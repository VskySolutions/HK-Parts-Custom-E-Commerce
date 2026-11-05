using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.GiftCertificates;
using HKPartsEcommerce.Infrastructure.Security;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Storefront gift certificate redemption on the caller's cart (cart session header / query, or the signed-in
/// customer's cart). Applying is rate-limited to 10 attempts per 15 minutes, per IP and per cart (429), and
/// every failure returns the same "Invalid Gift Certificate" so codes cannot be probed.
/// </summary>
[Route("api/cart")]
[AllowAnonymous]
public class CartGiftCertificateController : ApiControllerBase
{
    private const string SessionHeader = "X-Cart-Session";

    private readonly GiftCertificateApplyRateLimiter _limiter;

    public CartGiftCertificateController(GiftCertificateApplyRateLimiter limiter) => _limiter = limiter;

    public sealed record ApplyGiftCertificateBody(string? Code);

    [HttpPost("gift-certificate")]
    public async Task<ActionResult<CartGiftCertificateDto>> Apply([FromBody] ApplyGiftCertificateBody body, [FromQuery] string? sessionId = null)
    {
        var session = ResolveSession(sessionId);
        var cartKey = session ?? (User.Identity?.IsAuthenticated == true ? "user:" + User.Identity.Name : null);
        if (!_limiter.TryConsume(HttpContext.Connection.RemoteIpAddress?.ToString(), cartKey))
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                title = "Too many gift certificate attempts. Please wait a few minutes and try again.",
                status = StatusCodes.Status429TooManyRequests,
            });

        return Ok(await Mediator.Send(new ApplyCartGiftCertificateCommand(body.Code ?? string.Empty, session)));
    }

    [HttpDelete("gift-certificate")]
    public async Task<IActionResult> Remove([FromQuery] string? sessionId = null)
    {
        await Mediator.Send(new RemoveCartGiftCertificateCommand(ResolveSession(sessionId)));
        return NoContent();
    }

    /// <summary>Cart totals including <c>giftCertificate { code, currentBalance, applyAmount }</c> and <c>amountDue</c>.</summary>
    [HttpGet("totals")]
    public async Task<ActionResult<CartTotalsDto>> Totals([FromQuery] string? sessionId = null)
        => Ok(await Mediator.Send(new GetCartTotalsQuery(ResolveSession(sessionId))));

    private string? ResolveSession(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId;
        if (Request.Headers.TryGetValue(SessionHeader, out var header) && !string.IsNullOrWhiteSpace(header))
            return header.ToString();
        return null;
    }
}
