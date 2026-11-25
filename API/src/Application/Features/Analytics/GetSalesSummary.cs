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

    /// <summary>Guest checkout A-42: guest-checkout orders and revenue in the period, and their share of the totals
    /// (percent, 0–100). Always over all customer types, whatever the filter.</summary>
    public int GuestOrders { get; set; }
    public decimal GuestRevenue { get; set; }
    public decimal GuestOrderSharePercent { get; set; }
    public decimal GuestRevenueSharePercent { get; set; }

    /// <summary>The customer type the totals above were filtered to (All / Guest / Registered).</summary>
    public string CustomerType { get; set; } = "All";
}

/// <summary>
/// Totals for the dashboard KPI cards over the selected period (AC-ADM-001.1): order count, captured
/// revenue, average order value, and new customer registrations. Revenue counts only recognised (captured,
/// non-cancelled) orders placed in the window; new customers are genuine (Customer-role) accounts registered
/// in the window.
/// </summary>
public record GetSalesSummaryQuery(string? Period = null, DateTime? From = null, DateTime? To = null,
    /// <summary>Guest checkout A-41: "Guest", "Registered", or null / "All".</summary>
    string? CustomerType = null)
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

        var allOrders = _db.Orders.AsNoTracking()
            .Where(o => o.PlacedOnUtc >= period.StartUtc && o.PlacedOnUtc < period.EndUtc)
            .WithRecognisedRevenue();
        var orders = allOrders.OfCustomerType(request.CustomerType);

        var totalOrders = await orders.CountAsync(cancellationToken);
        var totalRevenue = await orders.SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        // A-42: the guest share is always against every recognised order in the period.
        var allCount = await allOrders.CountAsync(cancellationToken);
        var allRevenue = await allOrders.SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
        var guestOrders = allOrders.PlacedAsGuest();
        var guestCount = await guestOrders.CountAsync(cancellationToken);
        var guestRevenue = await guestOrders.SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
        static decimal Share(decimal part, decimal whole) =>
            whole == 0m ? 0m : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

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
            GuestOrders = guestCount,
            GuestRevenue = guestRevenue,
            GuestOrderSharePercent = Share(guestCount, allCount),
            GuestRevenueSharePercent = Share(guestRevenue, allRevenue),
            CustomerType = string.Equals(request.CustomerType, CustomerTypes.Guest, StringComparison.OrdinalIgnoreCase) ? CustomerTypes.Guest
                : string.Equals(request.CustomerType, CustomerTypes.Registered, StringComparison.OrdinalIgnoreCase) ? CustomerTypes.Registered
                : "All",
        };
    }
}
