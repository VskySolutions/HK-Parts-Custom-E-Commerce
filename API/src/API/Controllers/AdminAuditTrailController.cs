using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.AuditTrail;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Read-only admin audit-trail viewer (WO-61, REQ-ADM-003): surfaces recorded admin actions — entity type,
/// entity id, action, acting admin, and timestamp — with paging and filters. Per AC-ADM-003.3 the audit
/// trail is immutable, so this controller exposes no create/update/delete endpoints.
/// </summary>
[Route("api/admin/audit-trail")]
[RequireModule(Modules.AuditTrail)]
public class AdminAuditTrailController : ApiControllerBase
{
    /// <summary>
    /// List audit-trail entries (paged, newest first) filtered by date range, acting admin user, action
    /// type, affected entity type, and a free-text search term (actor / entity type / entity id).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<AuditTrailDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? search = null)
        => Ok(await Mediator.Send(new ListAuditTrailQuery(page, pageSize, dateFrom, dateTo, userId, action, entityType, search)));
}
