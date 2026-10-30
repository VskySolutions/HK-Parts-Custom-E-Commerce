using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Cart;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Storefront shopping cart (REQ-CHK-001). Anonymous by design — guests have carts too. A guest cart is
/// keyed by a client-supplied session id, accepted as the <c>sessionId</c> query parameter or the
/// <c>X-Cart-Session</c> header; an authenticated buyer's persisted cart is resolved from their identity
/// and restored on login (AC-CHK-001.4), ignoring any session id.
/// </summary>
[Route("api/cart")]
[AllowAnonymous]
public class CartController : ApiControllerBase
{
    private const string SessionHeader = "X-Cart-Session";

    /// <summary>Get the caller's active cart, creating an empty one if none exists.</summary>
    [HttpGet]
    public async Task<ActionResult<CartDto>> Get([FromQuery] string? sessionId = null)
        => Ok(await Mediator.Send(new GetCartQuery(ResolveSession(sessionId))));

    /// <summary>Add a product/variant to the cart (increments the quantity if the line already exists).</summary>
    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem([FromBody] AddItemCommand command, [FromQuery] string? sessionId = null)
        => Ok(await Mediator.Send(command with { SessionId = ResolveSession(sessionId) ?? command.SessionId }));

    /// <summary>Set a cart line's quantity (0 removes the line); the route item id wins over any id in the body.</summary>
    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<CartDto>> UpdateItem(
        Guid itemId, [FromBody] UpdateItemQuantityCommand command, [FromQuery] string? sessionId = null)
        => Ok(await Mediator.Send(command with { ItemId = itemId, SessionId = ResolveSession(sessionId) ?? command.SessionId }));

    /// <summary>Remove a single line from the cart.</summary>
    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartDto>> RemoveItem(Guid itemId, [FromQuery] string? sessionId = null)
        => Ok(await Mediator.Send(new RemoveItemCommand(itemId, ResolveSession(sessionId))));

    /// <summary>Empty the cart.</summary>
    [HttpDelete]
    public async Task<ActionResult<CartDto>> Clear([FromQuery] string? sessionId = null)
        => Ok(await Mediator.Send(new ClearCartCommand(ResolveSession(sessionId))));

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
