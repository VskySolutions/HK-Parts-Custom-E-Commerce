using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Tax;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Tax admin (REQ-TAX-002 + PHP parity): settings, state and ZIP rate tables, Avalara settings / test / log,
/// and US economic-nexus status. The tax report lives with the operational reports.
/// </summary>
[Route("api/admin/tax")]
[RequireModule(Modules.Tax)]
public class AdminTaxController : ApiControllerBase
{
    /// <summary>Get the current tax configuration, creating it with defaults if none exists.</summary>
    [HttpGet]
    public async Task<ActionResult<TaxConfigurationDto>> Get()
        => Ok(await Mediator.Send(new GetTaxConfigurationQuery()));

    /// <summary>Create or update the tax configuration (method, flat rate, tax shipping, Avalara settings).</summary>
    [HttpPut]
    public async Task<ActionResult<TaxConfigurationDto>> Update([FromBody] UpdateTaxConfigurationCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>US economic-nexus status per state (gross sales / transactions vs statutory thresholds).</summary>
    [HttpGet("nexus-status")]
    public async Task<ActionResult<List<NexusStateStatusDto>>> NexusStatus([FromQuery] int? year = null)
        => Ok(await Mediator.Send(new GetNexusStatusQuery(year)));

    // ---- State rates (PHP /echo/taxes/state) ----

    [HttpGet("state-rates")]
    public async Task<ActionResult<List<TaxStateRateDto>>> StateRates()
        => Ok(await Mediator.Send(new ListTaxStateRatesQuery()));

    [HttpPut("state-rates")]
    public async Task<ActionResult<List<TaxStateRateDto>>> UpdateStateRates([FromBody] UpdateTaxStateRatesCommand command)
        => Ok(await Mediator.Send(command));

    // ---- ZIP rates (PHP /echo/taxes/zip) ----

    [HttpGet("zip-rates")]
    public async Task<ActionResult<PaginatedList<TaxZipRateDto>>> ZipRates(
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await Mediator.Send(new ListTaxZipRatesQuery(search, page, pageSize)));

    [HttpPost("zip-rates")]
    public async Task<ActionResult<TaxZipRateDto>> CreateZipRate([FromBody] SaveTaxZipRateCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("zip-rates/{id:guid}")]
    public async Task<ActionResult<TaxZipRateDto>> UpdateZipRate(Guid id, [FromBody] SaveTaxZipRateCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("zip-rates/{id:guid}")]
    public async Task<IActionResult> DeleteZipRate(Guid id)
    {
        await Mediator.Send(new DeleteTaxZipRateCommand(id));
        return NoContent();
    }

    /// <summary>Bulk-load ZIP rates from a CSV file (zip,rate). Adds new ZIPs and updates existing ones.</summary>
    [HttpPost("zip-rates/import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<TaxZipImportResult>> ImportZipRates(IFormFile file, CancellationToken ct)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var text = await reader.ReadToEndAsync(ct);
        return Ok(await Mediator.Send(new ImportTaxZipRatesCommand(text), ct));
    }

    // ---- Avalara (PHP /echo/taxes/avalara + log) ----

    /// <summary>PHP Avalara account fields (service URL, account number, company code; licence key is write-only).</summary>
    [HttpGet("avalara/account")]
    public async Task<ActionResult<AvalaraAccountDto>> AvalaraAccount()
        => Ok(await Mediator.Send(new GetAvalaraAccountQuery()));

    /// <summary>Saves the Avalara account onto the active Avalara credential (blank licence key = keep).</summary>
    [HttpPut("avalara/account")]
    public async Task<ActionResult<AvalaraAccountDto>> SaveAvalaraAccount([FromBody] SaveAvalaraAccountCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Checks the active Avalara credential authenticates (AvaTax v2 ping). Always written to the log.</summary>
    [HttpPost("avalara/test")]
    public async Task<ActionResult<TaxConnectionTestResult>> TestAvalara()
        => Ok(await Mediator.Send(new TestAvalaraConnectionCommand()));

    [HttpGet("logs")]
    public async Task<ActionResult<PaginatedList<TaxLogListItemDto>>> Logs(
        [FromQuery] string? action = null, [FromQuery] bool? success = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await Mediator.Send(new ListTaxLogsQuery(action, success, search, page, pageSize)));

    [HttpGet("logs/{id:guid}")]
    public async Task<ActionResult<TaxLogDetailDto>> Log(Guid id)
        => Ok(await Mediator.Send(new GetTaxLogQuery(id)));

    /// <summary>Deletes log entries older than <c>olderThanDays</c> (0 = every entry). Returns the number removed.</summary>
    [HttpDelete("logs")]
    public async Task<ActionResult<int>> ClearLogs([FromQuery] int olderThanDays = 0)
        => Ok(await Mediator.Send(new ClearTaxLogsCommand(olderThanDays)));
}
