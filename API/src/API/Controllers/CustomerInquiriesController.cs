using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Inquiries;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>The authenticated customer's own quote requests (REQ-INQ-001).</summary>
[Route("api/customer/inquiries")]
[Authorize]
public class CustomerInquiriesController : ApiControllerBase
{
    /// <summary>List the current customer's own inquiries (paged), newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<InquirySummaryDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(new ListMyInquiriesQuery(page, pageSize), cancellationToken));
}
