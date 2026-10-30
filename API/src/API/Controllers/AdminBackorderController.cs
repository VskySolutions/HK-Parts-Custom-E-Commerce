using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Inventory;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Backorder configuration + fulfilment queue (REQ-CAT-013 / REQ-ORD-006). Per-product and per-variant
/// backorder toggles with an optional restock date, and a FIFO queue of open order lines awaiting stock
/// (with CSV export).
/// </summary>
[Route("api/admin/backorder")]
[RequireModule(Modules.Inventory)]
public class AdminBackorderController : ApiControllerBase
{
    /// <summary>Enable/disable backorders for a product and set an optional estimated restock date.</summary>
    [HttpPut("products/{id:guid}")]
    public async Task<ActionResult<BackorderConfigDto>> SetProduct(Guid id, [FromBody] SetProductBackorderCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Enable/disable backorders for a variant and set an optional estimated restock date.</summary>
    [HttpPut("variants/{variantId:guid}")]
    public async Task<ActionResult<BackorderConfigDto>> SetVariant(Guid variantId, [FromBody] SetVariantBackorderCommand command)
        => Ok(await Mediator.Send(command with { VariantId = variantId }));

    /// <summary>The backorder fulfilment queue (paged, FIFO), filterable by product, store and restock-by date.</summary>
    [HttpGet("queue")]
    public async Task<ActionResult<PaginatedList<BackorderQueueRowDto>>> Queue(
        [FromQuery] Guid? productId = null, [FromQuery] Guid? storeId = null, [FromQuery] DateTime? restockBy = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await Mediator.Send(new GetBackorderQueueQuery(productId, storeId, restockBy, page, pageSize)));

    /// <summary>Export the backorder queue as CSV.</summary>
    [HttpGet("queue/export")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? productId = null, [FromQuery] Guid? storeId = null, [FromQuery] DateTime? restockBy = null)
    {
        var csv = await Mediator.Send(new ExportBackorderQueueQuery(productId, storeId, restockBy));
        return File(csv, "text/csv", "backorder-queue.csv");
    }
}
