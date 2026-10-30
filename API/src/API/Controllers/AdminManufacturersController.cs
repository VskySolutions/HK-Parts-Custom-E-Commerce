using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Manufacturers;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage manufacturers/brands (SEO metadata, ordering, enable/disable).</summary>
[Route("api/admin/manufacturers")]
[RequireModule(Modules.Catalog)]
public class AdminManufacturersController : ApiControllerBase
{
    /// <summary>List manufacturers (paged), optionally filtered by name.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ManufacturerDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await Mediator.Send(new ListManufacturersQuery(page, pageSize, search)));

    /// <summary>Get a single manufacturer by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ManufacturerDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetManufacturerQuery(id)));

    /// <summary>Create a new manufacturer.</summary>
    [HttpPost]
    public async Task<ActionResult<ManufacturerDto>> Create([FromBody] CreateManufacturerCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing manufacturer (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManufacturerDto>> Update(Guid id, [FromBody] UpdateManufacturerCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a manufacturer.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteManufacturerCommand(id));
        return NoContent();
    }
}
