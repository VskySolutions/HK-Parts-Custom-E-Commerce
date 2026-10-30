using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Loyalty;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>The authenticated customer's own loyalty-points balance and ledger (WO-27).</summary>
[Route("api/customer/points")]
[Authorize]
public class CustomerLoyaltyController : ApiControllerBase
{
    /// <summary>The caller's points balance and recent transaction history.</summary>
    [HttpGet]
    public async Task<ActionResult<PointsBalanceDto>> GetMine()
        => Ok(await Mediator.Send(new GetMyPointsQuery()));
}
