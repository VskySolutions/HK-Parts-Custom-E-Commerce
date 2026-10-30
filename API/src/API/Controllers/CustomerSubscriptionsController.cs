using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Subscriptions;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The authenticated buyer's own product subscriptions (REQ-ORD-005): subscribe to a product, list their
/// subscriptions, and pause / resume / change interval / cancel. Every action is scoped to the caller's
/// own customer profile.
/// </summary>
[Route("api/customer/subscriptions")]
[Authorize]
public class CustomerSubscriptionsController : ApiControllerBase
{
    /// <summary>List the current customer's own subscriptions (paged), newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<SubscriptionDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new ListMySubscriptionsQuery(page, pageSize)));

    /// <summary>Subscribe to a product at a chosen recurrence interval.</summary>
    [HttpPost]
    public async Task<ActionResult<SubscriptionDto>> Create([FromBody] CreateSubscriptionCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Pause one of the current customer's own subscriptions.</summary>
    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<SubscriptionDto>> Pause(Guid id)
        => Ok(await Mediator.Send(new PauseSubscriptionCommand(id)));

    /// <summary>Resume one of the current customer's own paused subscriptions.</summary>
    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<SubscriptionDto>> Resume(Guid id)
        => Ok(await Mediator.Send(new ResumeSubscriptionCommand(id)));

    /// <summary>Change the recurrence interval of one of the current customer's own subscriptions (route id wins).</summary>
    [HttpPut("{id:guid}/interval")]
    public async Task<ActionResult<SubscriptionDto>> ChangeInterval(Guid id, [FromBody] ChangeSubscriptionIntervalCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Cancel one of the current customer's own subscriptions.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SubscriptionDto>> Cancel(Guid id)
        => Ok(await Mediator.Send(new CancelSubscriptionCommand(id)));
}
