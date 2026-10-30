using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Coupons;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage redeemable coupon codes bound to discounts (REQ-PRP-002).</summary>
[Route("api/admin/coupons")]
[RequireModule(Modules.Promotions)]
public class AdminCouponsController : ApiControllerBase
{
    /// <summary>List coupons (paged), optionally filtered by code, discount or active state.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<CouponDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? discountId = null,
        [FromQuery] bool? isActive = null)
        => Ok(await Mediator.Send(new ListCouponsQuery(page, pageSize, search, discountId, isActive)));

    /// <summary>Get a single coupon by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CouponDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetCouponQuery(id)));

    /// <summary>Create a new coupon code.</summary>
    [HttpPost]
    public async Task<ActionResult<CouponDto>> Create([FromBody] CreateCouponCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing coupon (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CouponDto>> Update(Guid id, [FromBody] UpdateCouponCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a coupon code.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCouponCommand(id));
        return NoContent();
    }
}
