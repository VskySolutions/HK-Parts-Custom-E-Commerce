using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.StoreManagers;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Admin management of store-manager assignments (WO-52, REQ-STR-004).</summary>
[Route("api/admin/store-managers")]
[RequireModule(Modules.Stores)]
public class AdminStoreManagersController : ApiControllerBase
{
    /// <summary>List assignments, optionally filtered to a single store.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StoreManagerAssignmentDto>>> List([FromQuery] Guid? storeId = null)
        => Ok(await Mediator.Send(new ListStoreManagersQuery(storeId)));

    /// <summary>Assign a user as manager of a store (upsert — one store per manager).</summary>
    [HttpPost]
    public async Task<ActionResult<StoreManagerAssignmentDto>> Assign([FromBody] AssignStoreManagerCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Remove an assignment.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id)
    {
        await Mediator.Send(new RemoveStoreManagerCommand(id));
        return NoContent();
    }
}
