using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Application.Features.Analytics;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Tax;

// ---- Avalara log (PHP TaxController avalaraLog / avalaraLogDetail) ------------------------------------------

public class TaxLogListItemDto
{
    public Guid Id { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? OrderNumber { get; set; }
    public int? HttpStatus { get; set; }
    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TaxLogDetailDto : TaxLogListItemDto
{
    public string? Url { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
}

/// <summary>The tax request log, newest first (PHP: 25 per page). Filter by action, outcome or order number.</summary>
public record ListTaxLogsQuery(
    string? Action = null, bool? Success = null, string? Search = null, int Page = 1, int PageSize = 25)
    : IRequest<PaginatedList<TaxLogListItemDto>>;

public class ListTaxLogsQueryHandler : IRequestHandler<ListTaxLogsQuery, PaginatedList<TaxLogListItemDto>>
{
    private readonly IApplicationDbContext _db;

    public ListTaxLogsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<TaxLogListItemDto>> Handle(ListTaxLogsQuery request, CancellationToken ct)
    {
        var query = _db.TaxLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(l => l.Action == request.Action);
        if (request.Success is bool success)
            query = query.Where(l => l.Success == success);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(l => l.OrderNumber != null && l.OrderNumber.Contains(term));
        }

        var projected = query
            .OrderByDescending(l => l.CreatedOnUtc)
            .Select(l => new TaxLogListItemDto
            {
                Id = l.Id,
                CreatedOnUtc = l.CreatedOnUtc,
                Provider = l.Provider,
                Action = l.Action,
                Success = l.Success,
                OrderNumber = l.OrderNumber,
                HttpStatus = l.HttpStatus,
                DurationMs = l.DurationMs,
                ErrorMessage = l.ErrorMessage,
            });
        return await PaginatedList<TaxLogListItemDto>.CreateAsync(
            projected, Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 200), ct);
    }
}

public record GetTaxLogQuery(Guid Id) : IRequest<TaxLogDetailDto>;

public class GetTaxLogQueryHandler : IRequestHandler<GetTaxLogQuery, TaxLogDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetTaxLogQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<TaxLogDetailDto> Handle(GetTaxLogQuery request, CancellationToken ct)
    {
        var l = await _db.TaxLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(TaxLog), request.Id);
        return new TaxLogDetailDto
        {
            Id = l.Id,
            CreatedOnUtc = l.CreatedOnUtc,
            Provider = l.Provider,
            Action = l.Action,
            Success = l.Success,
            OrderNumber = l.OrderNumber,
            HttpStatus = l.HttpStatus,
            DurationMs = l.DurationMs,
            ErrorMessage = l.ErrorMessage,
            Url = l.Url,
            RequestJson = l.RequestJson,
            ResponseJson = l.ResponseJson,
        };
    }
}

/// <summary>Deletes log entries older than <see cref="OlderThanDays"/> (0 = all). Returns how many were removed.</summary>
public record ClearTaxLogsCommand(int OlderThanDays = 0) : IRequest<int>;

public class ClearTaxLogsCommandHandler : IRequestHandler<ClearTaxLogsCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ClearTaxLogsCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<int> Handle(ClearTaxLogsCommand request, CancellationToken ct)
    {
        var cutoff = _clock.UtcNow.AddDays(-Math.Max(0, request.OlderThanDays));
        var rows = await _db.TaxLogs.Where(l => l.CreatedOnUtc < cutoff).ToListAsync(ct);
        _db.TaxLogs.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }
}

// ---- Avalara Test Connection (PHP avalaraSettingsPost pingTest) ---------------------------------------------

public record TestAvalaraConnectionCommand : IRequest<TaxConnectionTestResult>;

public class TestAvalaraConnectionCommandHandler : IRequestHandler<TestAvalaraConnectionCommand, TaxConnectionTestResult>
{
    private readonly IAvalaraTaxConnection _avalara;

    public TestAvalaraConnectionCommandHandler(IAvalaraTaxConnection avalara) => _avalara = avalara;

    public Task<TaxConnectionTestResult> Handle(TestAvalaraConnectionCommand request, CancellationToken ct)
        => _avalara.TestConnectionAsync(ct);
}

// ---- Tax Report (PHP ReportController taxes / EcommReporting::taxes) ----------------------------------------

public class TaxReportRowDto
{
    public string State { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public int Orders { get; set; }
    public int TaxedOrders { get; set; }
    /// <summary>Order totals before tax (merchandise − discounts + shipping).</summary>
    public decimal Sales { get; set; }
    /// <summary>Net tax: charged less what refunds handed back.</summary>
    public decimal Tax { get; set; }
    /// <summary>Tax handed back by refunds and returns (already taken off <see cref="Tax"/>).</summary>
    public decimal TaxRefunded { get; set; }
}

public class TaxReportDto
{
    /// <summary>PHP "Tax Collected": the sum of tax on the period's orders.</summary>
    public decimal TotalTax { get; set; }
    public decimal TotalTaxRefunded { get; set; }
    public int OrderCount { get; set; }
    public int TaxedOrderCount { get; set; }
    public decimal TotalSales { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public List<TaxReportRowDto> Rows { get; set; } = new();
}

/// <summary>
/// PHP "Tax Report": tax collected on the orders placed in the period, every status but cancelled (as PHP), with
/// a per ship-to-state breakdown on top of the PHP single total. Unpaid redirect shells and inquiries are left out.
/// </summary>
public record GetTaxReportQuery(string? Period = null, DateTime? From = null, DateTime? To = null) : IRequest<TaxReportDto>;

public class GetTaxReportQueryHandler : IRequestHandler<GetTaxReportQuery, TaxReportDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetTaxReportQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<TaxReportDto> Handle(GetTaxReportQuery request, CancellationToken ct)
    {
        var period = AnalyticsPeriod.Resolve(request.Period, request.From, request.To, _clock.UtcNow);
        var raw = await _db.Orders.AsNoTracking()
            .Where(o => o.PlacedOnUtc >= period.StartUtc && o.PlacedOnUtc < period.EndUtc)
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries()
            .Where(o => o.Status != OrderStatus.Cancelled)
            .GroupBy(o => new
            {
                State = o.ShippingAddress != null ? o.ShippingAddress.StateProvince : null,
                Country = o.ShippingAddress != null ? o.ShippingAddress.CountryCode : null,
            })
            .Select(g => new
            {
                g.Key.State,
                g.Key.Country,
                Orders = g.Count(),
                Taxed = g.Count(o => o.TaxTotal > 0m),
                Sales = g.Sum(o => o.TotalAmount - o.TaxTotal),
                Tax = g.Sum(o => o.TaxTotal - o.TaxRefundedTotal),
                TaxRefunded = g.Sum(o => o.TaxRefundedTotal),
            })
            .ToListAsync(ct);

        var rows = raw
            .GroupBy(r =>
            {
                var country = string.IsNullOrWhiteSpace(r.Country) ? null : r.Country.Trim().ToUpperInvariant();
                var state = string.IsNullOrWhiteSpace(r.State)
                    ? "Unknown"
                    : country is null or "US"
                        ? ShippingRulesEngine.NormalizeUsState(r.State) ?? r.State.Trim().ToUpperInvariant()
                        : r.State.Trim();
                return (State: state, Country: country);
            })
            .Select(g => new TaxReportRowDto
            {
                State = g.Key.State,
                CountryCode = g.Key.Country,
                Orders = g.Sum(x => x.Orders),
                TaxedOrders = g.Sum(x => x.Taxed),
                Sales = g.Sum(x => x.Sales),
                Tax = g.Sum(x => x.Tax),
                TaxRefunded = g.Sum(x => x.TaxRefunded),
            })
            .OrderByDescending(r => r.Tax)
            .ThenBy(r => r.State)
            .ToList();

        return new TaxReportDto
        {
            TotalTax = rows.Sum(r => r.Tax),
            TotalTaxRefunded = rows.Sum(r => r.TaxRefunded),
            OrderCount = rows.Sum(r => r.Orders),
            TaxedOrderCount = rows.Sum(r => r.TaxedOrders),
            TotalSales = rows.Sum(r => r.Sales),
            FromUtc = period.StartUtc,
            ToUtc = period.EndUtc,
            Rows = rows,
        };
    }

    public static byte[] ToCsv(TaxReportDto report)
    {
        static string N(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        var rows = report.Rows
            .Select(r => new string?[] { r.State, r.CountryCode, I(r.Orders), I(r.TaxedOrders), N(r.Sales), N(r.TaxRefunded), N(r.Tax) })
            .Append(new string?[] { "Total", null, I(report.OrderCount), I(report.TaxedOrderCount), N(report.TotalSales), N(report.TotalTaxRefunded), N(report.TotalTax) });
        var csv = Csv.Write(new[] { "State", "Country", "Orders", "Taxed orders", "Sales (pre-tax)", "Tax refunded", "Net tax collected" }, rows);
        return Encoding.UTF8.GetBytes(csv);
    }
}
