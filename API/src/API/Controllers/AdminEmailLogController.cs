using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.EmailLog;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Admin Email Log: browse the full history of queued/sent emails and re-send any of them. Backed by
/// the <c>EmailQueue</c> table that the EmailDispatchWorker delivers from.
/// </summary>
[Route("api/admin/email-log")]
[RequireModule(Modules.EmailLog)]
public class AdminEmailLogController : ApiControllerBase
{
    /// <summary>Paged email history (newest first), filterable by search, status and category.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<EmailLogItemDto>>> List([FromQuery] ListEmailLogQuery query)
        => Ok(await Mediator.Send(query));

    /// <summary>The full record for one email, including the rendered body and last error.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmailLogDetailDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetEmailLogQuery(id)));

    /// <summary>Re-queue an email for delivery (creates a fresh Pending copy; the original stays as history).</summary>
    [HttpPost("{id:guid}/resend")]
    public async Task<ActionResult<EmailLogDetailDto>> Resend(Guid id)
        => Ok(await Mediator.Send(new ResendEmailCommand(id)));
}
