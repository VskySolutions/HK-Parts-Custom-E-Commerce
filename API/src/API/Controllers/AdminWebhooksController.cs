using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Webhooks;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage webhook subscriptions and inspect delivery history (REQ-PLT-003).</summary>
[Route("api/admin/webhooks")]
[RequireModule(Modules.Webhooks)]
public class AdminWebhooksController : ApiControllerBase
{
    /// <summary>Register an endpoint with a URL + subscribed event types. Returns the signing secret once.</summary>
    [HttpPost("subscriptions")]
    public async Task<ActionResult<WebhookSubscriptionDto>> Create([FromBody] CreateWebhookSubscriptionCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>List registered endpoints (secrets are not returned).</summary>
    [HttpGet("subscriptions")]
    public async Task<ActionResult<List<WebhookSubscriptionDto>>> List()
        => Ok(await Mediator.Send(new ListWebhookSubscriptionsQuery()));

    /// <summary>Update an endpoint's URL, subscribed events, description and active state (the secret is preserved).</summary>
    [HttpPut("subscriptions/{id:guid}")]
    public async Task<ActionResult<WebhookSubscriptionDto>> Update(Guid id, [FromBody] UpdateWebhookSubscriptionCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>The catalog of event types a subscription can subscribe to.</summary>
    [HttpGet("event-types")]
    public ActionResult<IReadOnlyList<string>> EventTypes()
        => Ok(WebhookEventTypes.All);

    /// <summary>Remove an endpoint.</summary>
    [HttpDelete("subscriptions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteWebhookSubscriptionCommand(id));
        return NoContent();
    }

    /// <summary>Delivery history (response status + attempt count), optionally for one subscription.</summary>
    [HttpGet("deliveries")]
    public async Task<ActionResult<PaginatedList<WebhookDeliveryDto>>> Deliveries(
        [FromQuery] Guid? subscriptionId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await Mediator.Send(new ListWebhookDeliveriesQuery(subscriptionId, page, pageSize)));
}
