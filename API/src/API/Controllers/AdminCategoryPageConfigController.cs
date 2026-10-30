using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.CmsCategoryConfig;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage a category's dynamic storefront page configuration (WO-99): banner, promotional
/// description, pinned products and the "You May Also Like" collection. One config per category.</summary>
[Route("api/admin/category-page-configs")]
[RequireModule(Modules.Cms)]
public class AdminCategoryPageConfigController : ApiControllerBase
{
    /// <summary>Get a category's page config (an empty/default shell when none exists yet).</summary>
    [HttpGet("{categoryId:guid}")]
    public async Task<ActionResult<CmsCategoryPageConfigDto>> Get(Guid categoryId)
        => Ok(await Mediator.Send(new GetCategoryPageConfigQuery(categoryId)));

    /// <summary>Create or update a category's page config (route category id wins over any id in the body).</summary>
    [HttpPut("{categoryId:guid}")]
    public async Task<ActionResult<CmsCategoryPageConfigDto>> Upsert(Guid categoryId, [FromBody] UpsertCategoryPageConfigCommand command)
        => Ok(await Mediator.Send(command with { CategoryId = categoryId }));

    /// <summary>Delete a category's page config (hard delete; pinned children cascade away).</summary>
    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> Delete(Guid categoryId)
    {
        await Mediator.Send(new DeleteCategoryPageConfigCommand(categoryId));
        return NoContent();
    }
}
