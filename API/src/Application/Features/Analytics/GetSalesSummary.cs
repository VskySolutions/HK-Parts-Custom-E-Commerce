using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Analytics;

/// <summary>KPI summary for the dashboard cards over a resolved period (AC-ADM-001.1).</summary>
public class SalesSummaryDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int NewCustomers { get; set; }
}

/// <summary>
/// Totals for the dashboard KPI cards over the selected period (AC-ADM-001.1): order count, captured
/// revenue, average order value, and new customer registrations. Revenue counts only recognised (captured,
/// non-cancelled) orders placed in the window; new customers are genuine (Customer-role) accounts registered
/// in the window.
/// </summary>
public record GetSalesSummaryQuery(string? Period = null, DateTime? From = null, DateTime? To = null)
    : IRequest<SalesSummaryDto>;

public class GetSalesSummaryQueryHandler : IRequestHandler<GetSalesSummaryQuery, SalesSummaryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetSalesSummaryQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<SalesSummaryDto> Handle(GetSalesSummaryQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriod.Resolve(request.Period, request.From, request.To, _clock.UtcNow);

        var orders = _db.Orders.AsNoTracking()
            .Where(o => o.PlacedOnUtc >= period.StartUtc && o.PlacedOnUtc < period.EndUtc)
            .WithRecognisedRevenue();

        var totalOrders = await orders.CountAsync(cancellationToken);
        var totalRevenue = await orders.SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var customerRole = nameof(RoleType.Customer);
        var newCustomers = await _db.Customers.AsNoTracking()
            .Where(c => c.CreatedOnUtc >= period.StartUtc && c.CreatedOnUtc < period.EndUtc
                        && c.User != null && c.User.UserRoles.Any(ur => ur.Role!.Name == customerRole))
            .CountAsync(cancellationToken);

        return new SalesSummaryDto
        {
            FromUtc = period.StartUtc,
            ToUtc = period.EndUtc,
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            AverageOrderValue = totalOrders == 0
                ? 0m
                : Math.Round(totalRevenue / totalOrders, 2, MidpointRounding.AwayFromZero),
            NewCustomers = newCustomers,
        };
    }
}
