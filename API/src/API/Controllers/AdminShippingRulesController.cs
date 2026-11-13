using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Shipping;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Shipping rules: signature / insurance add-on fees and destination restrictions (AD-20 / AD-21).</summary>
[Route("api/admin/shipping/rules")]
[RequireModule(Modules.Shipping)]
public class AdminShippingRulesController : ApiControllerBase
{
    /// <summary>Get the shipping rules (the legacy defaults until first saved).</summary>
    [HttpGet]
    public async Task<ActionResult<ShippingRulesDto>> Get()
        => Ok(await Mediator.Send(new GetShippingRulesQuery()));

    /// <summary>Save the shipping rules. Each save bumps the version recorded on new orders.</summary>
    [HttpPut]
    public async Task<ActionResult<ShippingRulesDto>> Update([FromBody] UpdateShippingRulesCommand command)
        => Ok(await Mediator.Send(command));
}
