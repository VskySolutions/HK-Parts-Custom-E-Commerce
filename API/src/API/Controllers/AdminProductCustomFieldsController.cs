using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.ProductCustomFields;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage merchant-defined free-text product fields (values are set per product).</summary>
[Route("api/admin/product-custom-fields")]
[RequireModule(Modules.Catalog)]
public class AdminProductCustomFieldsController : ApiControllerBase
{
    /// <summary>List all custom fields, optionally only active (or inactive) ones.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ProductCustomFieldDto>>> List([FromQuery] bool? isActive = null)
        => Ok(await Mediator.Send(new ListProductCustomFieldsQuery(isActive)));

    /// <summary>Create a custom field.</summary>
    [HttpPost]
    public async Task<ActionResult<ProductCustomFieldDto>> Create([FromBody] CreateProductCustomFieldCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update a custom field (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductCustomFieldDto>> Update(Guid id, [FromBody] UpdateProductCustomFieldCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a custom field; its product values are removed.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteProductCustomFieldCommand(id));
        return NoContent();
    }
}
