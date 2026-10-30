using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Localization;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Configure the tenant's supported storefront languages (REQ-STF-004).</summary>
[Route("api/tenant/languages")]
[RequireModule(Modules.Languages)]
public class TenantLanguagesController : ApiControllerBase
{
    /// <summary>List all configured languages.</summary>
    [HttpGet]
    public async Task<ActionResult<List<LanguageDto>>> List()
        => Ok(await Mediator.Send(new ListLanguagesQuery()));

    /// <summary>Add a supported language.</summary>
    [HttpPost]
    public async Task<ActionResult<LanguageDto>> Create([FromBody] CreateLanguageCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update a language (name, native name, enabled, order).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LanguageDto>> Update(Guid id, [FromBody] UpdateLanguageCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Set the default (fallback) language.</summary>
    [HttpPut("{id:guid}/default")]
    public async Task<ActionResult<LanguageDto>> SetDefault(Guid id)
        => Ok(await Mediator.Send(new SetDefaultLanguageCommand(id)));

    /// <summary>Remove a language (not the default).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteLanguageCommand(id));
        return NoContent();
    }
}
