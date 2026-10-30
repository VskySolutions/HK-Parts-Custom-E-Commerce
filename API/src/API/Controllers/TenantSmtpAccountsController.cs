using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.SmtpAccounts;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage SMTP accounts. Passwords are encrypted at rest and never returned.</summary>
[Route("api/tenant/smtp-accounts")]
[RequireModule(Modules.SmtpAccounts)]
public class TenantSmtpAccountsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SmtpAccountDto>>> List()
        => Ok(await Mediator.Send(new ListSmtpAccountsQuery()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SmtpAccountDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetSmtpAccountQuery(id)));

    [HttpPost]
    public async Task<ActionResult<SmtpAccountDto>> Create([FromBody] CreateSmtpAccountCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SmtpAccountDto>> Update(Guid id, [FromBody] UpdateSmtpAccountCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteSmtpAccountCommand(id));
        return NoContent();
    }

    /// <summary>Send a test email through this account.</summary>
    [HttpPost("{id:guid}/test-send")]
    public async Task<ActionResult<ConnectivityTestResult>> TestSend(Guid id, [FromBody] TestSendRequest body)
        => Ok(await Mediator.Send(new TestSendSmtpAccountCommand(id, body.ToEmail)));

    public record TestSendRequest(string ToEmail);
}
