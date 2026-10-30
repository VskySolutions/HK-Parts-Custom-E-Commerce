using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Localization;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Author translations for entity content (REQ-STF-004). Keyed by (entity type, entity id).</summary>
[Route("api/admin/translations")]
[RequireModule(Modules.Languages)]
public class AdminTranslationsController : ApiControllerBase
{
    /// <summary>Get all authored translations for one entity.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ContentTranslationDto>>> Get([FromQuery] string entityType, [FromQuery] Guid entityId)
        => Ok(await Mediator.Send(new GetContentTranslationsQuery(entityType, entityId)));

    /// <summary>Upsert the translations for one entity in one language (blank value removes a field).</summary>
    [HttpPut]
    public async Task<ActionResult<List<ContentTranslationDto>>> Set([FromBody] SetContentTranslationsCommand command)
        => Ok(await Mediator.Send(command));
}
