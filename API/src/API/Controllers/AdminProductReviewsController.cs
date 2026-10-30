using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.ProductReviews;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Moderate customer product reviews and toggle per-product review acceptance (WO-14).</summary>
[Route("api/admin/product-reviews")]
[RequireModule(Modules.Catalog)]
public class AdminProductReviewsController : ApiControllerBase
{
    /// <summary>List reviews (paged), filterable by status, product, rating, date range and free-text term.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ProductReviewDto>>> List(
        [FromQuery] ReviewStatus? status = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] int? rating = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListProductReviewsQuery(
            status, productId, rating, dateFrom, dateTo, search, page, pageSize, sortBy, sortDescending)));

    /// <summary>Moderation-queue counts (pending/approved/rejected/total), optionally scoped to one product.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ProductReviewStatsDto>> Stats([FromQuery] Guid? productId = null)
        => Ok(await Mediator.Send(new GetProductReviewStatsQuery(productId)));

    /// <summary>Approve or reject a single review (route id wins over any id in the body).</summary>
    [HttpPost("{id:guid}/moderate")]
    public async Task<ActionResult<ProductReviewDto>> Moderate(Guid id, [FromBody] ModerateProductReviewCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Approve or reject many reviews at once; returns the number updated.</summary>
    [HttpPost("bulk-moderate")]
    public async Task<ActionResult<int>> BulkModerate([FromBody] BulkModerateProductReviewsCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Enable or disable review acceptance/display for a product (route id wins over any id in the body).</summary>
    [HttpPut("products/{productId:guid}/reviews-enabled")]
    public async Task<IActionResult> SetReviewsEnabled(Guid productId, [FromBody] SetProductReviewsEnabledCommand command)
    {
        await Mediator.Send(command with { ProductId = productId });
        return NoContent();
    }
}
