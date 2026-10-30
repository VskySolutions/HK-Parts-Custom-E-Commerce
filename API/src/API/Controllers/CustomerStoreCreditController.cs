using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Customers;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>The authenticated customer's own store-credit balance and ledger (WO-48, REQ-ORD-004).</summary>
[Route("api/customer/store-credit")]
[Authorize]
public class CustomerStoreCreditController : ApiControllerBase
{
    /// <summary>The caller's store-credit balance and transaction history.</summary>
    [HttpGet]
    public async Task<ActionResult<StoreCreditDto>> GetMine()
        => Ok(await Mediator.Send(new GetMyStoreCreditQuery()));
}
