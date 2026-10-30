using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Analytics;

/// <summary>One day on the sales trend line: the day (UTC midnight) plus its order count and captured revenue.</summary>
public class SalesTrendPointDto
{
    public DateTime Date { get; set; }
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
}

/// <summary>
/// Order count + captured revenue grouped by day over the selected period (AC-ADM-001.2). Days with no
/// recognised orders are returned as zero-valued points so the series is contiguous for charting.
/// </summary>
public record GetSalesTrendQuery(string? Period = null, DateTime? From = null, DateTime? To = null)
    : IRequest<IReadOnlyList<SalesTrendPointDto>>;

public class GetSalesTrendQueryHandler : IRequestHandler<GetSalesTrendQuery, IReadOnlyList<SalesTrendPointDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetSalesTrendQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SalesTrendPointDto>> Handle(GetSalesTrendQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriod.Resolve(request.Period, request.From, request.To, _clock.UtcNow);

        var byDay = await _db.Orders.AsNoTracking()
            .Where(o => o.PlacedOnUtc >= period.StartUtc && o.PlacedOnUtc < period.EndUtc)
            .WithRecognisedRevenue()
            .GroupBy(o => o.PlacedOnUtc.Date)
            .Select(g => new SalesTrendPointDto
            {
                Date = g.Key,
                OrderCount = g.Count(),
                Revenue = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        var lookup = byDay.ToDictionary(p => p.Date);

        // Emit a contiguous day-by-day series (gaps as zero) across the whole window for a clean chart line.
        var series = new List<SalesTrendPointDto>();
        for (var day = period.StartUtc.Date; day < period.EndUtc; day = day.AddDays(1))
            series.Add(lookup.TryGetValue(day, out var point)
                ? point
                : new SalesTrendPointDto { Date = day });

        return series;
    }
}
