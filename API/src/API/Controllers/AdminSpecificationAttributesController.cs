using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.SpecificationAttributes;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage the global specification attribute library used for storefront faceted navigation.</summary>
[Route("api/admin/specification-attributes")]
[RequireModule(Modules.Catalog)]
public class AdminSpecificationAttributesController : ApiControllerBase
{
    /// <summary>List specification attributes (paged), optionally filtered by name and/or filterable state.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<SpecificationAttributeDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] bool? isFilterable = null)
        => Ok(await Mediator.Send(new ListSpecificationAttributesQuery(page, pageSize, search, isFilterable)));

    /// <summary>Get a single specification attribute (including its options) by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpecificationAttributeDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetSpecificationAttributeQuery(id)));

    /// <summary>Create a new specification attribute with its options.</summary>
    [HttpPost]
    public async Task<ActionResult<SpecificationAttributeDto>> Create([FromBody] CreateSpecificationAttributeCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing specification attribute and reconcile its options (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SpecificationAttributeDto>> Update(Guid id, [FromBody] UpdateSpecificationAttributeCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a specification attribute.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteSpecificationAttributeCommand(id));
        return NoContent();
    }
}
