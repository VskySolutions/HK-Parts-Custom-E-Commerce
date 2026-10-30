using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Reports;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Admin cross-store reporting (REQ-STR-005). Omit <c>storeId</c> to report on all stores.</summary>
[Route("api/admin/reports")]
[RequireModule(Modules.Orders)]
public class AdminReportsController : ApiControllerBase
{
    /// <summary>Per-store performance for a period: orders received/fulfilled, revenue, avg fulfilment time.</summary>
    [HttpGet("store-performance")]
    public async Task<ActionResult<StorePerformanceReportDto>> StorePerformance(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] Guid? storeId = null)
        => Ok(await Mediator.Send(new StorePerformanceReportQuery(storeId, from, to, ManagerScoped: false)));
}
