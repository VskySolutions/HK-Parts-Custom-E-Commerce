using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Currencies;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage the catalogue of display currencies and their exchange rates.</summary>
[Route("api/tenant/currencies")]
public class TenantCurrenciesController : ApiControllerBase
{
    /// <summary>List currencies, optionally restricting to the enabled ones.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> List([FromQuery] bool? enabledOnly)
        => Ok(await Mediator.Send(new ListCurrenciesQuery(enabledOnly)));

    /// <summary>Get a single currency by ISO 4217 code.</summary>
    [HttpGet("{code}")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrencyDto>> Get(string code)
        => Ok(await Mediator.Send(new GetCurrencyQuery(code)));

    /// <summary>Get the exchange-rate auto-refresh configuration.</summary>
    [HttpGet("auto-refresh")]
    [RequireModule(Modules.Currencies)]
    public async Task<ActionResult<AutoRefreshConfigDto>> GetAutoRefreshConfig()
        => Ok(await Mediator.Send(new GetAutoRefreshConfigQuery()));

    /// <summary>Update the exchange-rate auto-refresh configuration.</summary>
    [HttpPut("auto-refresh")]
    [RequireModule(Modules.Currencies)]
    public async Task<ActionResult<AutoRefreshConfigDto>> UpdateAutoRefreshConfig([FromBody] UpdateAutoRefreshConfigRequest body)
        => Ok(await Mediator.Send(new UpdateAutoRefreshConfigCommand(body.Enabled, body.IntervalHours, body.SourceUrl)));

    /// <summary>Add a new display currency.</summary>
    [HttpPost]
    [RequireModule(Modules.Currencies)]
    public async Task<ActionResult<CurrencyDto>> Create([FromBody] CreateCurrencyRequest body)
        => Ok(await Mediator.Send(new CreateCurrencyCommand(body.CurrencyCode, body.Symbol, body.ExchangeRate, body.IsEnabled)));

    /// <summary>Update an existing currency.</summary>
    [HttpPut("{code}")]
    [RequireModule(Modules.Currencies)]
    public async Task<ActionResult<CurrencyDto>> Update(string code, [FromBody] UpdateCurrencyRequest body)
        => Ok(await Mediator.Send(new UpdateCurrencyCommand(code, body.Symbol, body.ExchangeRate, body.IsEnabled, body.IsRateLocked)));

    /// <summary>Delete a currency (the base currency cannot be deleted).</summary>
    [HttpDelete("{code}")]
    [RequireModule(Modules.Currencies)]
    public async Task<IActionResult> Delete(string code)
    {
        await Mediator.Send(new DeleteCurrencyCommand(code));
        return NoContent();
    }

    public record CreateCurrencyRequest(string CurrencyCode, string Symbol, decimal ExchangeRate, bool IsEnabled);
    public record UpdateCurrencyRequest(string Symbol, decimal ExchangeRate, bool IsEnabled, bool IsRateLocked);
    public record UpdateAutoRefreshConfigRequest(bool Enabled, int IntervalHours, string? SourceUrl);
}

/// <summary>Read and set the platform base currency that all exchange rates are relative to.</summary>
[Route("api/tenant/base-currency")]
public class TenantBaseCurrencyController : ApiControllerBase
{
    /// <summary>Get the current base currency.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<CurrencyDto>> Get()
        => Ok(await Mediator.Send(new GetBaseCurrencyQuery()));

    /// <summary>Promote a currency to be the base currency.</summary>
    [HttpPut]
    [Authorize(Policy = Policies.SuperAdmin)]
    public async Task<ActionResult<CurrencyDto>> Set([FromBody] SetBaseCurrencyRequest body)
        => Ok(await Mediator.Send(new SetBaseCurrencyCommand(body.Code)));

    public record SetBaseCurrencyRequest(string Code);
}
