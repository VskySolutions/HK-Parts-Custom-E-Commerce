using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.ProductQuestions;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public (anonymous) product Q&amp;A surface (WO-58): list the approved, answered questions for a
/// product and submit a new question. Submission is protected by the optional reCAPTCHA QaSubmit hook.
/// </summary>
[Route("api/storefront/products/{productId:guid}/questions")]
[AllowAnonymous]
public class StorefrontProductQuestionsController : ApiControllerBase
{
    /// <summary>Approved, answered questions for a product (newest first).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductQuestionPublicDto>>> List(Guid productId)
        => Ok(await Mediator.Send(new GetProductQuestionsQuery(productId)));

    /// <summary>Submit a new question about a product (created Pending, awaiting admin moderation).</summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Submit(Guid productId, [FromBody] SubmitProductQuestionCommand command)
        => Ok(await Mediator.Send(command with { ProductId = productId }));
}
