using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>Per-state economic-nexus status: totals vs statutory thresholds and how close each is (AC-TAX-004.4).</summary>
public class NexusStateStatusDto
{
    public string StateCode { get; set; } = string.Empty;
    public int PeriodYear { get; set; }
    public decimal GrossSales { get; set; }
    public int TransactionCount { get; set; }
    public decimal ThresholdAmount { get; set; }
    public int? ThresholdTransactions { get; set; }

    /// <summary>The higher of the revenue and transaction-count fractions (1.0 = at threshold).</summary>
    public decimal PercentToThreshold { get; set; }
    public bool Approaching { get; set; }
    public bool Exceeded { get; set; }
    public DateTime? LastAlertedAtUtc { get; set; }
}

/// <summary>Returns nexus status per US state, most-advanced first (AC-TAX-004.4).</summary>
public record GetNexusStatusQuery(int? Year = null) : IRequest<List<NexusStateStatusDto>>;

public class GetNexusStatusQueryHandler : IRequestHandler<GetNexusStatusQuery, List<NexusStateStatusDto>>
{
    private readonly IApplicationDbContext _db;

    public GetNexusStatusQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<NexusStateStatusDto>> Handle(GetNexusStatusQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StateNexusAccumulators.AsNoTracking().AsQueryable();
        if (request.Year is int year)
            query = query.Where(a => a.PeriodStartUtc.Year == year);

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .Select(a =>
            {
                var revenueFraction = a.ThresholdAmount > 0 ? a.GrossSales / a.ThresholdAmount : 0m;
                var countFraction = a.ThresholdTransactions is int tt && tt > 0
                    ? (decimal)a.TransactionCount / tt
                    : 0m;
                var percent = Math.Max(revenueFraction, countFraction);
                return new NexusStateStatusDto
                {
                    StateCode = a.StateCode,
                    PeriodYear = a.PeriodStartUtc.Year,
                    GrossSales = a.GrossSales,
                    TransactionCount = a.TransactionCount,
                    ThresholdAmount = a.ThresholdAmount,
                    ThresholdTransactions = a.ThresholdTransactions,
                    PercentToThreshold = Math.Round(percent, 4),
                    Approaching = percent >= a.WarningPercent,
                    Exceeded = percent >= 1m,
                    LastAlertedAtUtc = a.LastAlertedAtUtc,
                };
            })
            .OrderByDescending(s => s.PercentToThreshold)
            .ThenBy(s => s.StateCode)
            .ToList();
    }
}
