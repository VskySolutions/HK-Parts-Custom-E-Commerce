using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Analytics;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Sales analytics + revenue dashboard endpoints (WO-59, REQ-ADM-001).</summary>
[Route("api/admin/analytics")]
[RequireModule(Modules.Dashboard)]
public class AdminAnalyticsController : ApiControllerBase
{
    /// <summary>KPI summary — orders, revenue, average order value, new customers — for the period (AC-ADM-001.1).</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<SalesSummaryDto>> Summary(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        => Ok(await Mediator.Send(new GetSalesSummaryQuery(period, from, to)));

    /// <summary>Order count + revenue by day over the period (AC-ADM-001.2).</summary>
    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<SalesTrendPointDto>>> Trend(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        => Ok(await Mediator.Send(new GetSalesTrendQuery(period, from, to)));

    /// <summary>The most recent orders with their status (AC-ADM-001.3).</summary>
    [HttpGet("recent-orders")]
    public async Task<ActionResult<IReadOnlyList<RecentOrderDto>>> RecentOrders([FromQuery] int take = 10)
        => Ok(await Mediator.Send(new GetRecentOrdersQuery(take)));
}
