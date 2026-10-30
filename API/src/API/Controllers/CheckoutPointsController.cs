using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Loyalty;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Loyalty-points redemption on the checkout cart (WO-27). Authenticated: only a signed-in customer can
/// redeem points. Shares the <c>api/checkout</c> route prefix with the main checkout surface via distinct
/// action routes. The guest cart session id (for storefront session carts) is read from the
/// <c>sessionId</c> query parameter or the <c>X-Cart-Session</c> header.
/// </summary>
[Route("api/checkout")]
[Authorize]
public class CheckoutPointsController : ApiControllerBase
{
    private const string SessionHeader = "X-Cart-Session";

    /// <summary>Stage a points redemption on the caller's active cart (sets the points discount).</summary>
    [HttpPost("apply-points")]
    public async Task<ActionResult<ApplyPointsResult>> ApplyPoints(
        [FromBody] ApplyPointsCommand command, [FromQuery] string? sessionId = null)
        => Ok(await Mediator.Send(command with { SessionId = ResolveSession(sessionId) ?? command.SessionId }));

    /// <summary>Clear any staged points redemption from the caller's active cart.</summary>
    [HttpDelete("remove-points")]
    public async Task<IActionResult> RemovePoints([FromQuery] string? sessionId = null)
    {
        await Mediator.Send(new RemovePointsCommand(ResolveSession(sessionId)));
        return NoContent();
    }

    /// <summary>Resolves the guest cart session id, preferring the query string over the X-Cart-Session header.</summary>
    private string? ResolveSession(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId;
        if (Request.Headers.TryGetValue(SessionHeader, out var header) && !string.IsNullOrWhiteSpace(header))
            return header.ToString();
        return null;
    }
}
