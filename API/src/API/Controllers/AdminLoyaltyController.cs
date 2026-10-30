using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Loyalty;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage the loyalty reward-points program configuration: on/off + earn/redeem rates (WO-27).</summary>
[Route("api/admin/loyalty")]
[RequireModule(Modules.Promotions)]
public class AdminLoyaltyController : ApiControllerBase
{
    /// <summary>Get the current loyalty configuration (defaults applied when unset).</summary>
    [HttpGet]
    public async Task<ActionResult<LoyaltyConfigDto>> Get()
        => Ok(await Mediator.Send(new GetLoyaltyConfigQuery()));

    /// <summary>Update the loyalty configuration (enabled flag, earn rate, redeem rate).</summary>
    [HttpPut]
    public async Task<ActionResult<LoyaltyConfigDto>> Update([FromBody] UpdateLoyaltyConfigCommand command)
        => Ok(await Mediator.Send(command));
}
