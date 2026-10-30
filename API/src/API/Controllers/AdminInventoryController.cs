using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Inventory;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage per-store inventory levels, adjustments and RMA receipts (REQ-CAT-011).</summary>
[Route("api/admin/inventory")]
[RequireModule(Modules.Inventory)]
public class AdminInventoryController : ApiControllerBase
{
    /// <summary>List inventory levels (paged), optionally filtered by product and/or store.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<InventoryLevelDto>>> List(
        [FromQuery] Guid? productId = null, [FromQuery] Guid? storeId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new ListInventoryQuery(productId, storeId, page, pageSize)));

    /// <summary>Get every stock level held for a product across all stores.</summary>
    [HttpGet("{productId:guid}/levels")]
    public async Task<ActionResult<IReadOnlyList<InventoryLevelDto>>> GetForProduct(Guid productId)
        => Ok(await Mediator.Send(new GetProductInventoryQuery(productId)));

    /// <summary>Create or update the stock level and optional low-stock threshold for a product/variant at a store.</summary>
    [HttpPut("levels")]
    public async Task<ActionResult<InventoryLevelDto>> Upsert([FromBody] UpsertInventoryLevelCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Apply a relative stock adjustment.</summary>
    [HttpPost("adjust")]
    public async Task<ActionResult<InventoryLevelDto>> Adjust([FromBody] AdjustInventoryLevelCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Add accepted returned units back to stock (RMA path).</summary>
    [HttpPost("mark-received")]
    public async Task<ActionResult<InventoryLevelDto>> MarkReceived([FromBody] MarkReceivedCommand command)
        => Ok(await Mediator.Send(command));
}
