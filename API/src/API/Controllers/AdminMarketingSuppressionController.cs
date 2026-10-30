using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.MarketingSuppression;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Admin view of the Marketing Suppression List (WO-87 AC-ENT-006.5): a paged/searchable list and a CSV
/// export. There is deliberately no "remove from suppression" action — re-subscription requires the
/// recipient's own explicit opt-in and no admin bulk action may override it (AC-ENT-006.6).
/// </summary>
[Route("api/admin/marketing-suppression")]
[RequireModule(Modules.EmailTemplates)]
public class AdminMarketingSuppressionController : ApiControllerBase
{
    /// <summary>List suppressed recipients (paged), optionally filtered by an email search term.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<MarketingSuppressionDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListMarketingSuppressionQuery(page, pageSize, search, sortBy, sortDescending)));

    /// <summary>Export the entire Marketing Suppression List as a CSV download.</summary>
    [HttpGet("export.csv")]
    public async Task<IActionResult> Export()
    {
        var bytes = await Mediator.Send(new ExportMarketingSuppressionQuery());
        return File(bytes, "text/csv", "marketing-suppression.csv");
    }
}
