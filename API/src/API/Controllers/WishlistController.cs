using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Wishlist;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The authenticated customer's wishlist (REQ-CHK-002): save products/variants for later, remove them,
/// and move them straight into the cart. Adding to a wishlist never reserves stock (AC-CHK-002.3).
/// </summary>
[Route("api/wishlist")]
[Authorize]
public class WishlistController : ApiControllerBase
{
    /// <summary>Get the current customer's wishlist (creates an empty one if none exists).</summary>
    [HttpGet]
    public async Task<ActionResult<WishlistDto>> Get()
        => Ok(await Mediator.Send(new GetWishlistQuery()));

    /// <summary>Add a product/variant to the wishlist (idempotent).</summary>
    [HttpPost("items")]
    public async Task<ActionResult<WishlistDto>> AddItem([FromBody] AddWishlistItemCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Remove an item from the wishlist.</summary>
    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<WishlistDto>> RemoveItem(Guid itemId)
        => Ok(await Mediator.Send(new RemoveWishlistItemCommand(itemId)));

    /// <summary>Move a wishlist item into the cart and remove it from the wishlist.</summary>
    [HttpPost("items/{itemId:guid}/move-to-cart")]
    public async Task<ActionResult<WishlistDto>> MoveToCart(Guid itemId, [FromBody] MoveWishlistItemToCartCommand? command = null)
        => Ok(await Mediator.Send((command ?? new MoveWishlistItemToCartCommand(itemId)) with { ItemId = itemId }));
}
