using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.InventoryScanner;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Barcode inventory scanner (port of the PHP admin's "Update Inventory" scanner): look a product up by
/// scanned barcode / typed SKU, update its stock and price, and keep an audit trail of every change.
/// <para>
/// Access: the Inventory module opens the scanner and saves stock. Changing the selling price also needs
/// the Catalog module (the spec's optional Inventory.UpdatePrice split); without it the price is
/// read-only in the UI and a price change is rejected with 403. SuperAdmin / TenantAdmin bypass both.
/// </para>
/// </summary>
[Route("api/admin/inventory-scanner")]
[RequireModule(Modules.Inventory)]
public class AdminInventoryScannerController : ApiControllerBase
{
    private const int MaxUserAgentLength = 300;

    private readonly IAuthorizationService _authorization;

    public AdminInventoryScannerController(IAuthorizationService authorization) => _authorization = authorization;

    /// <summary>Products updated today (tenant display timezone), last update time and the most recent updates.</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<InventoryScannerDashboardDto>> Dashboard([FromQuery] int take = 10)
        => Ok(await Mediator.Send(new GetInventoryScannerDashboardQuery(take)));

    /// <summary>
    /// Resolve a scanned code (SKU or GTIN, product then variant). The code travels in the query string
    /// because Code 39 / Code 128 labels may contain '/', '+', '%' or spaces. 400 empty / too long,
    /// 404 no match, 409 several matches (body carries <c>candidates</c>).
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<ScannedProductDto>> Lookup([FromQuery] string? code)
    {
        // "Not found" / "several match" are normal scanner outcomes: answered directly, never thrown.
        var result = await Mediator.Send(new LookupScannedProductQuery(code ?? string.Empty));
        if (result.IsAmbiguous)
            return Conflict(new { title = result.Message, message = result.Message, status = 409, candidates = result.Candidates });
        if (result.Product is null)
            return NotFound(new { title = result.Message, message = result.Message, status = 404 });

        result.Product.CanChangePrice = await CanChangePriceAsync();
        return Ok(result.Product);
    }

    /// <summary>The product screen, by id (variant via <paramref name="variantId"/>). 404 when it doesn't exist.</summary>
    [HttpGet("products/{productId:guid}")]
    public async Task<ActionResult<ScannedProductDto>> GetProduct(Guid productId, [FromQuery] Guid? variantId)
    {
        var dto = await Mediator.Send(new GetScannerProductQuery(productId, variantId));
        if (dto is null)
            return NotFound(new { title = "Product not found.", message = "Product not found.", status = 404 });

        dto.CanChangePrice = await CanChangePriceAsync();
        return Ok(dto);
    }

    /// <summary>Paged scanner history for one product (or one of its variants), newest first.</summary>
    [HttpGet("products/{productId:guid}/history")]
    public async Task<ActionResult<PaginatedList<InventoryScanLogDto>>> History(
        Guid productId, [FromQuery] Guid? variantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new GetInventoryScanHistoryQuery(productId, variantId, page, pageSize)));

    /// <summary>
    /// Save a stock and/or price change; returns the audit row with the product's new state.
    /// 400 invalid / nothing changed, 403 price change without Catalog access, 404 unknown product,
    /// 409 the product changed since it was shown (body carries <c>currentQty</c> / <c>currentPrice</c>).
    /// The acting user, client IP and user agent come from the request, never the body.
    /// </summary>
    [HttpPost("update")]
    public async Task<ActionResult<InventoryScanLogDto>> Update([FromBody] UpdateScannedInventoryCommand command)
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        return Ok(await Mediator.Send(command with
        {
            CanChangePrice = await CanChangePriceAsync(),
            // Behind a proxy this is the forwarded client address once UseForwardedHeaders is configured.
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = userAgent.Length > MaxUserAgentLength ? userAgent[..MaxUserAgentLength] : userAgent,
        }));
    }

    /// <summary>One update (the success screen), with the product's current state.</summary>
    [HttpGet("logs/{id:guid}")]
    public async Task<ActionResult<InventoryScanLogDto>> Log(Guid id)
        => Ok(await Mediator.Send(new GetInventoryScanLogQuery(id)));

    private async Task<bool> CanChangePriceAsync()
        => (await _authorization.AuthorizeAsync(User, resource: null, ModulePolicyProvider.Prefix + Modules.Catalog)).Succeeded;
}
