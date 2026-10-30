using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.ShippingMethods;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage custom (non-carrier) shipping methods and their per-zone rates (WO-43).</summary>
[Route("api/admin/shipping-methods")]
[RequireModule(Modules.Shipping)]
public class AdminShippingMethodsController : ApiControllerBase
{
    /// <summary>List shipping methods (paged), optionally filtered by name, calculation type and/or enabled state.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ShippingMethodDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] ShippingMethodType? methodType = null, [FromQuery] bool? isEnabled = null)
        => Ok(await Mediator.Send(new ListShippingMethodsQuery(page, pageSize, search, methodType, isEnabled)));

    /// <summary>Get a single shipping method by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShippingMethodDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetShippingMethodQuery(id)));

    /// <summary>Create a new shipping method.</summary>
    [HttpPost]
    public async Task<ActionResult<ShippingMethodDto>> Create([FromBody] CreateShippingMethodCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing shipping method (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShippingMethodDto>> Update(Guid id, [FromBody] UpdateShippingMethodCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a shipping method.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteShippingMethodCommand(id));
        return NoContent();
    }
}
