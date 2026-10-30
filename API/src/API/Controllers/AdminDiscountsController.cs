using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Discounts;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage configurable discount rules (REQ-PRP-001).</summary>
[Route("api/admin/discounts")]
[RequireModule(Modules.Promotions)]
public class AdminDiscountsController : ApiControllerBase
{
    /// <summary>List discounts (paged), optionally filtered by name, scope or active state.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<DiscountDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DiscountScope? scope = null,
        [FromQuery] bool? isActive = null)
        => Ok(await Mediator.Send(new ListDiscountsQuery(page, pageSize, search, scope, isActive)));

    /// <summary>Get a single discount by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DiscountDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetDiscountQuery(id)));

    /// <summary>Create a new discount rule.</summary>
    [HttpPost]
    public async Task<ActionResult<DiscountDto>> Create([FromBody] CreateDiscountCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing discount (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DiscountDto>> Update(Guid id, [FromBody] UpdateDiscountCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a discount rule.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteDiscountCommand(id));
        return NoContent();
    }
}
