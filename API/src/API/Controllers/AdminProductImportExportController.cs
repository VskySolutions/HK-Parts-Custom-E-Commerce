using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Bulk product import/export (REQ-CAT-009). Shares the <c>api/admin/products</c> prefix with the main
/// products controller (distinct action templates).
/// </summary>
[Route("api/admin/products")]
[RequireModule(Modules.Catalog)]
public class AdminProductImportExportController : ApiControllerBase
{
    /// <summary>
    /// Import products from a CSV upload. All-or-nothing: on any row error nothing is written and the failing
    /// rows are returned (HTTP 200 with Success=false + Errors).
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ImportResultDto>> Import(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty CSV file is required.");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        return Ok(await Mediator.Send(new ImportProductsCommand(stream.ToArray())));
    }

    /// <summary>Export the catalog (or a filtered subset) as CSV.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] ProductType? type = null, [FromQuery] bool? isPublished = null, [FromQuery] Guid? categoryId = null)
    {
        var file = await Mediator.Send(new ExportProductsQuery(type, isPublished, categoryId));
        return File(file.Content, file.ContentType, file.FileName);
    }
}
