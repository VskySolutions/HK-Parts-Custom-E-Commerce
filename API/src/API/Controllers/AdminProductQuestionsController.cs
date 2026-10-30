using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.ProductQuestions;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Admin moderation of storefront product questions (WO-58): list/filter, answer, approve/reject.</summary>
[Route("api/admin/product-questions")]
[RequireModule(Modules.Cms)]
public class AdminProductQuestionsController : ApiControllerBase
{
    /// <summary>List product questions (paged), filterable by moderation status, product, and free-text search.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ProductQuestionDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] QuestionStatus? status = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListProductQuestionsQuery(page, pageSize, status, productId, search, sortBy, sortDescending)));

    /// <summary>Author a pre-answered FAQ for a product; published immediately (Approved + answered).</summary>
    [HttpPost]
    public async Task<ActionResult<ProductQuestionDto>> Create([FromBody] CreateProductFaqCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Answer a question. Does not change its moderation status (approve separately).</summary>
    [HttpPost("{id:guid}/answer")]
    public async Task<ActionResult<ProductQuestionDto>> Answer(Guid id, [FromBody] AnswerProductQuestionCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Approve or reject a question (route id wins over any id in the body).</summary>
    [HttpPost("{id:guid}/moderate")]
    public async Task<ActionResult<ProductQuestionDto>> Moderate(Guid id, [FromBody] ModerateProductQuestionCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));
}
