using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Audit;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Cross-cutting record audit metadata (Created/Updated by + timestamps) shown at the bottom of every
/// admin detail page. Because it spans modules, access is resolved per entity type against that entity's
/// owning module (reusing the standard <c>module:</c> policy): a caller sees audit info only for modules
/// they can already reach, and storefront customers — who carry no module claims — are denied.
/// </summary>
[Route("api/admin/records")]
[Authorize]
public class AdminRecordAuditController : ApiControllerBase
{
    /// <summary>Get the creation/modification actor + timestamps for a single registered record.</summary>
    [HttpGet("{entityType}/{id:guid}/audit")]
    public async Task<ActionResult<RecordAuditDto>> GetAudit(string entityType, Guid id)
    {
        if (!RecordAuditRegistry.TryGet(entityType, out _, out var module))
            return NotFound();

        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(User, resource: null, ModulePolicyProvider.Prefix + module);
        if (!result.Succeeded)
            return Forbid();

        return Ok(await Mediator.Send(new GetRecordAuditQuery(entityType, id)));
    }
}
