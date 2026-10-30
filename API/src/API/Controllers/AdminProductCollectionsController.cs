using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.CmsProductCollections;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage admin-curated product collections (WO-97): collection CRUD plus the ordered item set that
/// backs home page Product Rows and category "You May Also Like" rows.</summary>
[Route("api/admin/product-collections")]
[RequireModule(Modules.Cms)]
public class AdminProductCollectionsController : ApiControllerBase
{
    /// <summary>List collections (paged), optionally filtered by name/slug and enabled state.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<CmsProductCollectionListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListCmsProductCollectionsQuery(page, pageSize, search, isEnabled, sortBy, sortDescending)));

    /// <summary>Get a single collection with its ordered items (product name, SKU, primary image per row).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CmsProductCollectionDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetCmsProductCollectionQuery(id)));

    /// <summary>Create a new (empty) collection.</summary>
    [HttpPost]
    public async Task<ActionResult<CmsProductCollectionDto>> Create([FromBody] CreateCmsProductCollectionCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update a collection's metadata (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CmsProductCollectionDto>> Update(Guid id, [FromBody] UpdateCmsProductCollectionCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a collection.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCmsProductCollectionCommand(id));
        return NoContent();
    }

    /// <summary>Append a product to the collection (route id wins over any collection id in the body).</summary>
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<CmsProductCollectionDto>> AddItem(Guid id, [FromBody] AddProductToCollectionCommand command)
        => Ok(await Mediator.Send(command with { CollectionId = id }));

    /// <summary>Remove a product from the collection.</summary>
    [HttpDelete("{id:guid}/items/{productId:guid}")]
    public async Task<ActionResult<CmsProductCollectionDto>> RemoveItem(Guid id, Guid productId)
        => Ok(await Mediator.Send(new RemoveProductFromCollectionCommand(id, productId)));

    /// <summary>Rewrite the collection's item order to match the supplied product-id sequence.</summary>
    [HttpPut("{id:guid}/items/reorder")]
    public async Task<ActionResult<CmsProductCollectionDto>> Reorder(Guid id, [FromBody] ReorderCollectionItemsCommand command)
        => Ok(await Mediator.Send(command with { CollectionId = id }));
}
