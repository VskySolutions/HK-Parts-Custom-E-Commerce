using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Subscriptions;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Admin management of subscriptions across all customers (WO-49 admin note): view all subscriptions
/// (paged, filterable by customer and status) and pause or cancel one on a customer's behalf.
/// </summary>
[Route("api/admin/subscriptions")]
[RequireModule(Modules.Orders)]
public class AdminSubscriptionsController : ApiControllerBase
{
    /// <summary>List subscriptions across customers (paged), optionally filtered by customer and status.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<SubscriptionDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? customerId = null,
        [FromQuery] SubscriptionStatus? status = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListSubscriptionsQuery(page, pageSize, customerId, status, sortBy, sortDescending)));

    /// <summary>Pause a customer's subscription on their behalf.</summary>
    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<SubscriptionDto>> Pause(Guid id)
        => Ok(await Mediator.Send(new AdminPauseSubscriptionCommand(id)));

    /// <summary>Cancel a customer's subscription on their behalf.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SubscriptionDto>> Cancel(Guid id)
        => Ok(await Mediator.Send(new AdminCancelSubscriptionCommand(id)));
}
