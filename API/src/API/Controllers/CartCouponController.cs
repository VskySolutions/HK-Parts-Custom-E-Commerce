using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Coupons;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Storefront coupon actions on a cart (REQ-PRP-002): apply and remove a coupon code. Anonymous so
/// guest carts are supported. The rest of the cart surface lives on a separate controller.
/// </summary>
[Route("api/cart")]
[AllowAnonymous]
public class CartCouponController : ApiControllerBase
{
    /// <summary>Validate and apply a coupon code to a cart; an invalid code is rejected (AC-PRP-002.3).</summary>
    [HttpPost("apply-coupon")]
    public async Task<ActionResult<ApplyCouponResult>> ApplyCoupon([FromBody] ApplyCouponCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Remove any coupon code currently applied to a cart.</summary>
    [HttpDelete("remove-coupon")]
    public async Task<IActionResult> RemoveCoupon([FromBody] RemoveCouponCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }
}
