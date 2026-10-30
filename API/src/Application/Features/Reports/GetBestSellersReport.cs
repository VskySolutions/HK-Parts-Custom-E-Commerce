using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Analytics;

namespace HKPartsEcommerce.Application.Features.Reports;

/// <summary>A best-sellers row: a product with its units sold and revenue over the period (AC-ADM-002.1).</summary>
public class BestSellerRowDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

/// <summary>
/// Ranks products by units sold (then revenue) over the selected period, across recognised (captured,
/// non-cancelled) orders (AC-ADM-002.1). Product names are resolved from the current catalog, falling back to
/// the order-line snapshot for products that no longer exist.
/// </summary>
public record GetBestSellersReportQuery(
    string? Period = null,
    DateTime? From = null,
    DateTime? To = null,
    int Take = 50) : IRequest<IReadOnlyList<BestSellerRowDto>>;

public class GetBestSellersReportQueryHandler : IRequestHandler<GetBestSellersReportQuery, IReadOnlyList<BestSellerRowDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetBestSellersReportQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BestSellerRowDto>> Handle(GetBestSellersReportQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriod.Resolve(request.Period, request.From, request.To, _clock.UtcNow);
        var take = Math.Clamp(request.Take, 1, 500);

        var windowOrders = _db.Orders.AsNoTracking()
            .Where(o => o.PlacedOnUtc >= period.StartUtc && o.PlacedOnUtc < period.EndUtc)
            .WithRecognisedRevenue();

        // Aggregate line items belonging to those orders by product, ranked by units then revenue.
        var grouped = await _db.OrderLineItems.AsNoTracking()
            .Where(li => windowOrders.Any(o => o.Id == li.OrderId))
            .GroupBy(li => li.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                UnitsSold = g.Sum(li => li.Quantity),
                Revenue = g.Sum(li => (decimal?)li.LineTotal) ?? 0m,
                // Fallback snapshot name for products no longer in the catalog (MAX is SQL-translatable).
                SnapshotName = g.Max(li => li.ProductName),
            })
            .OrderByDescending(x => x.UnitsSold)
            .ThenByDescending(x => x.Revenue)
            .Take(take)
            .ToListAsync(cancellationToken);

        var ids = grouped.Select(x => x.ProductId).ToList();
        var names = await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return grouped.Select(x => new BestSellerRowDto
        {
            ProductId = x.ProductId,
            ProductName = names.TryGetValue(x.ProductId, out var name) ? name : (x.SnapshotName ?? "(unknown product)"),
            UnitsSold = x.UnitsSold,
            Revenue = x.Revenue,
        }).ToList();
    }
}
