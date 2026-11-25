using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Reports;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Operational reports — best sellers, low stock, customers — with CSV export (WO-60, REQ-ADM-002). Shares
/// the <c>api/admin/reports</c> prefix with <see cref="AdminReportsController"/> (store-performance); the
/// action templates are distinct so the two controllers coexist.
/// </summary>
[Route("api/admin/reports")]
[RequireModule(Modules.Dashboard)]
public class AdminOperationalReportsController : ApiControllerBase
{
    /// <summary>Best-selling products ranked by units sold then revenue for the period (AC-ADM-002.1).</summary>
    [HttpGet("best-sellers")]
    public async Task<ActionResult<IReadOnlyList<BestSellerRowDto>>> BestSellers(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] int take = 50)
        => Ok(await Mediator.Send(new GetBestSellersReportQuery(period, from, to, take)));

    /// <summary>Best sellers exported as CSV (AC-ADM-002.4).</summary>
    [HttpGet("best-sellers.csv")]
    public async Task<IActionResult> BestSellersCsv(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] int take = 50)
    {
        var rows = await Mediator.Send(new GetBestSellersReportQuery(period, from, to, take));
        return File(ReportCsv.BestSellers(rows), "text/csv", "best-sellers.csv");
    }

    /// <summary>Inventory at or below its configured low-stock threshold (AC-ADM-002.2).</summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyList<LowStockRowDto>>> LowStock()
        => Ok(await Mediator.Send(new GetLowStockReportQuery()));

    /// <summary>Low stock exported as CSV (AC-ADM-002.4).</summary>
    [HttpGet("low-stock.csv")]
    public async Task<IActionResult> LowStockCsv()
    {
        var rows = await Mediator.Send(new GetLowStockReportQuery());
        return File(ReportCsv.LowStock(rows), "text/csv", "low-stock.csv");
    }

    /// <summary>New registrations, total active customers, and top customers by order value (AC-ADM-002.3).</summary>
    [HttpGet("customers")]
    public async Task<ActionResult<CustomersReportDto>> Customers(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] int topCount = 10)
        => Ok(await Mediator.Send(new GetCustomersReportQuery(period, from, to, topCount)));

    /// <summary>Top customers exported as CSV (AC-ADM-002.4).</summary>
    [HttpGet("customers.csv")]
    public async Task<IActionResult> CustomersCsv(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] int topCount = 10)
    {
        var report = await Mediator.Send(new GetCustomersReportQuery(period, from, to, topCount));
        return File(ReportCsv.Customers(report), "text/csv", "customers.csv");
    }

    /// <summary>Guest checkout A-40/A-41: sales by ship-to state with a guest / registered split. customerType = All / Guest / Registered.</summary>
    [HttpGet("sales-by-state")]
    public async Task<ActionResult<IReadOnlyList<SalesByStateRowDto>>> SalesByState(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] string? customerType = null)
        => Ok(await Mediator.Send(new GetSalesByStateReportQuery(period, from, to, customerType)));

    [HttpGet("sales-by-state.csv")]
    public async Task<IActionResult> SalesByStateCsv(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] string? customerType = null)
    {
        var rows = await Mediator.Send(new GetSalesByStateReportQuery(period, from, to, customerType));
        return File(GetSalesByStateReportQueryHandler.ToCsv(rows), "text/csv", "sales-by-state.csv");
    }

    /// <summary>PHP "Tax Report": tax collected on the period's non-cancelled orders, with a per-state breakdown.</summary>
    [HttpGet("taxes")]
    public async Task<ActionResult<HKPartsEcommerce.Application.Features.Tax.TaxReportDto>> Taxes(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        => Ok(await Mediator.Send(new HKPartsEcommerce.Application.Features.Tax.GetTaxReportQuery(period, from, to)));

    [HttpGet("taxes.csv")]
    public async Task<IActionResult> TaxesCsv(
        [FromQuery] string? period = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var report = await Mediator.Send(new HKPartsEcommerce.Application.Features.Tax.GetTaxReportQuery(period, from, to));
        return File(HKPartsEcommerce.Application.Features.Tax.GetTaxReportQueryHandler.ToCsv(report), "text/csv", "taxes.csv");
    }

    /// <summary>Guest checkout A-40/A-43: open carts idle for <c>idleHours</c>, guest carts included with the email they typed.</summary>
    [HttpGet("abandoned-carts")]
    public async Task<ActionResult<PaginatedList<AbandonedCartDto>>> AbandonedCarts(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] int idleHours = 1,
        [FromQuery] string? customerType = null, [FromQuery] string? search = null, [FromQuery] bool onlyWithEmail = false)
        => Ok(await Mediator.Send(new ListAbandonedCartsQuery(page, pageSize, idleHours, customerType, search, onlyWithEmail)));

    [HttpGet("abandoned-carts.csv")]
    public async Task<IActionResult> AbandonedCartsCsv(
        [FromQuery] int idleHours = 1, [FromQuery] string? customerType = null, [FromQuery] string? search = null,
        [FromQuery] bool onlyWithEmail = false)
    {
        var bytes = await Mediator.Send(new ExportAbandonedCartsQuery(new ListAbandonedCartsQuery(1, 1, idleHours, customerType, search, onlyWithEmail)));
        return File(bytes, "text/csv", "abandoned-carts.csv");
    }
}
