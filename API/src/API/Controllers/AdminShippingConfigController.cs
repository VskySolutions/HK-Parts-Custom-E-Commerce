using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Shipping;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage the singleton shipping provider configuration (REQ-SHP-005).</summary>
[Route("api/admin/shipping/config")]
[RequireModule(Modules.Shipping)]
public class AdminShippingConfigController : ApiControllerBase
{
    /// <summary>Get the current shipping configuration, creating it with every source enabled if none exists.</summary>
    [HttpGet]
    public async Task<ActionResult<ShippingConfigurationDto>> Get()
        => Ok(await Mediator.Send(new GetShippingConfigurationQuery()));

    /// <summary>Create or update the shipping configuration (master switch + enabled rate sources).</summary>
    [HttpPut]
    public async Task<ActionResult<ShippingConfigurationDto>> Update([FromBody] UpdateShippingConfigurationCommand command)
        => Ok(await Mediator.Send(command));
}
