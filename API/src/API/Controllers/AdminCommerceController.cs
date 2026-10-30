using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.Commerce;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Tenant commerce mode (REQ-INQ-001): whether the storefront sells with payment (Standard) or collects
/// inquiries only. Guarded by the Settings module — this switch decides whether the shop can take money.
/// </summary>
[Route("api/admin/commerce")]
[RequireModule(Modules.Settings)]
public class AdminCommerceController : ApiControllerBase
{
    /// <summary>Read the tenant's commerce-mode configuration.</summary>
    [HttpGet("mode")]
    public async Task<ActionResult<CommerceModeDto>> Get(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetCommerceModeQuery(), cancellationToken));

    /// <summary>Switch the commerce mode and its inquiry options (audited; applies without a restart).</summary>
    [HttpPut("mode")]
    public async Task<ActionResult<CommerceModeDto>> Update(
        [FromBody] UpdateCommerceModeCommand command, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(command, cancellationToken));
}
