using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Stores;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage store/fulfilment location configuration.</summary>
[Route("api/admin/stores")]
[RequireModule(Modules.Stores)]
public class AdminStoresController : ApiControllerBase
{
    /// <summary>List stores (paged), optionally filtered by name, enabled state, guest-ordering and/or pickup availability.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<StoreDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] bool? isEnabled = null, [FromQuery] bool? guestOrderingEnabled = null, [FromQuery] bool? pickupEnabled = null)
        => Ok(await Mediator.Send(new ListStoresQuery(page, pageSize, search, isEnabled, guestOrderingEnabled, pickupEnabled)));

    /// <summary>Get a single store by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StoreDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetStoreQuery(id)));

    /// <summary>Create a new store.</summary>
    [HttpPost]
    public async Task<ActionResult<StoreDto>> Create([FromBody] CreateStoreCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing store (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StoreDto>> Update(Guid id, [FromBody] UpdateStoreCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a store.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteStoreCommand(id));
        return NoContent();
    }
}
