using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Logging;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Read-only admin Log Viewer (WO-70): surfaces persisted Error/Fatal application logs with paging and
/// filters. Each row carries a trimmed message plus correlation metadata only — never a stack trace.
/// </summary>
[Route("api/admin/logs")]
[RequireModule(Modules.Logs)]
public class AdminLogViewerController : ApiControllerBase
{
    /// <summary>
    /// List application logs (paged, newest first) filtered by date range, exact level, correlation id,
    /// and a message search term. Defaults to Error + Fatal severities when no level is supplied.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ApplicationLogDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? level = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] string? search = null)
        => Ok(await Mediator.Send(new ListApplicationLogsQuery(page, pageSize, dateFrom, dateTo, level, correlationId, search)));
}
