using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.ProductAttributes;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage the global product attribute library used for variant generation.</summary>
[Route("api/admin/product-attributes")]
[RequireModule(Modules.Catalog)]
public class AdminProductAttributesController : ApiControllerBase
{
    /// <summary>List product attributes (paged), optionally filtered by name and/or display type.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ProductAttributeDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] ProductAttributeDisplayType? displayType = null)
        => Ok(await Mediator.Send(new ListProductAttributesQuery(page, pageSize, search, displayType)));

    /// <summary>Get a single product attribute (including its values) by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductAttributeDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetProductAttributeQuery(id)));

    /// <summary>Create a new product attribute with its values.</summary>
    [HttpPost]
    public async Task<ActionResult<ProductAttributeDto>> Create([FromBody] CreateProductAttributeCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing product attribute and reconcile its values (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductAttributeDto>> Update(Guid id, [FromBody] UpdateProductAttributeCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a product attribute.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteProductAttributeCommand(id));
        return NoContent();
    }
}
