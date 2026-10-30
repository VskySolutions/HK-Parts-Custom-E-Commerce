using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Settings;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Read and update DB-backed platform settings.</summary>
[Route("api/admin/settings")]
[RequireModule(Modules.Settings)]
public class AdminSettingsController : ApiControllerBase
{
    /// <summary>List settings, optionally filtered by category.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> List([FromQuery] string? category)
        => Ok(await Mediator.Send(new GetSettingsQuery(category)));

    /// <summary>Change history, optionally filtered by setting key.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<SettingHistoryDto>>> History([FromQuery] string? key, [FromQuery] int take = 100)
        => Ok(await Mediator.Send(new GetSettingHistoryQuery(key, take)));

    /// <summary>Get a single setting.</summary>
    [HttpGet("{key}")]
    public async Task<ActionResult<SettingDto>> Get(string key)
        => Ok(await Mediator.Send(new GetSettingQuery(key)));

    /// <summary>Update a single setting.</summary>
    [HttpPut("{key}")]
    public async Task<ActionResult<SettingDto>> Update(string key, [FromBody] UpdateSettingRequest body)
        => Ok(await Mediator.Send(new UpdateSettingCommand(key, body.Value)));

    /// <summary>Bulk-update multiple settings.</summary>
    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> UpdateMany([FromBody] UpdateSettingsRequest body)
        => Ok(await Mediator.Send(new UpdateSettingsCommand(body.Settings)));

    public record UpdateSettingRequest(string? Value);
    public record UpdateSettingsRequest(IReadOnlyDictionary<string, string?> Settings);
}
