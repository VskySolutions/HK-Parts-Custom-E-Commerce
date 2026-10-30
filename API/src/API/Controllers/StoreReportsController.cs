using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Reports;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// A store manager's own performance report (REQ-STR-005). Auto-scoped to the caller's assigned store —
/// a manager can never see another store's figures (AC-STR-005.2).
/// </summary>
[Route("api/store/reports")]
[Authorize]
public class StoreReportsController : ApiControllerBase
{
    /// <summary>Performance for the caller's store over a period.</summary>
    [HttpGet("store-performance")]
    public async Task<ActionResult<StorePerformanceReportDto>> StorePerformance(
        [FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await Mediator.Send(new StorePerformanceReportQuery(null, from, to, ManagerScoped: true)));
}
