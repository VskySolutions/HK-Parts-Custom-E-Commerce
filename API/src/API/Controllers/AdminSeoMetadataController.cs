using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Seo;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Central per-record SEO metadata for every module (products, categories, manufacturers, CMS pages, blog
/// posts, tags, collections). Access is checked against the module that owns the entity type
/// (<see cref="SeoEntities.ModuleFor"/>), so a Catalog-only role can edit product SEO but not CMS page SEO.
/// </summary>
[Authorize]
[Route("api/admin/seo/entities/{entityType}/{entityId:guid}")]
public class AdminSeoMetadataController : ApiControllerBase
{
    private readonly IAuthorizationService _authorization;

    public AdminSeoMetadataController(IAuthorizationService authorization) => _authorization = authorization;

    /// <summary>Get a record's SEO metadata (empty payload when none has been saved yet).</summary>
    [HttpGet]
    public async Task<ActionResult<SeoMetadataDto>> Get(SeoEntityType entityType, Guid entityId)
    {
        if (!await CanAccessAsync(entityType)) return Forbid();
        return Ok(await Mediator.Send(new GetSeoMetadataQuery(entityType, entityId)));
    }

    /// <summary>Create or replace a record's SEO metadata.</summary>
    [HttpPut]
    public async Task<ActionResult<SeoMetadataDto>> Upsert(
        SeoEntityType entityType, Guid entityId, [FromBody] UpsertSeoMetadataCommand command)
    {
        if (!await CanAccessAsync(entityType)) return Forbid();
        return Ok(await Mediator.Send(command with { EntityType = entityType, EntityId = entityId }));
    }

    private async Task<bool> CanAccessAsync(SeoEntityType entityType)
    {
        if (!Enum.IsDefined(entityType)) return false;
        var policy = ModulePolicyProvider.Prefix + SeoEntities.ModuleFor(entityType);
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }
}
