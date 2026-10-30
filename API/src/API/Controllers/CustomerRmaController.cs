using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Rma;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>The authenticated customer's returns (REQ-ORD-004): request a return, list their own returns.</summary>
[Route("api/customer/rmas")]
[Authorize]
public class CustomerRmaController : ApiControllerBase
{
    /// <summary>Request a return for one of the caller's delivered orders.</summary>
    [HttpPost]
    public async Task<ActionResult<RmaDto>> Submit([FromBody] SubmitRmaCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>List the caller's own returns.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<RmaDto>>> ListMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new ListMyRmasQuery(page, pageSize)));
}
