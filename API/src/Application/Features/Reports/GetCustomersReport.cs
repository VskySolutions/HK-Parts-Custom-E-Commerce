using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Analytics;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Reports;

/// <summary>A top-customer row: identity plus lifetime paid order count and value (AC-ADM-002.3).</summary>
public class TopCustomerRowDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
}

/// <summary>The customers report: registration + active counts plus the top customers by lifetime value.</summary>
public class CustomersReportDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int NewRegistrations { get; set; }
    public int TotalActiveCustomers { get; set; }
    public List<TopCustomerRowDto> TopCustomers { get; set; } = new();
}

/// <summary>
/// New registrations in the period, total active (non-deleted) customers, and the top customers by lifetime
/// recognised order value (AC-ADM-002.3). "Customer" means a User carrying the Customer system role, so staff
/// accounts (which also get a Customer profile at creation) are excluded — matching the admin customer list.
/// The top-customers ranking is all-time (lifetime), independent of the period window.
/// </summary>
public record GetCustomersReportQuery(
    string? Period = null,
    DateTime? From = null,
    DateTime? To = null,
    int TopCount = 10) : IRequest<CustomersReportDto>;

public class GetCustomersReportQueryHandler : IRequestHandler<GetCustomersReportQuery, CustomersReportDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetCustomersReportQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CustomersReportDto> Handle(GetCustomersReportQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriod.Resolve(request.Period, request.From, request.To, _clock.UtcNow);
        var topCount = Math.Clamp(request.TopCount, 1, 100);
        var customerRole = nameof(RoleType.Customer);

        // Genuine customers only (carry the Customer role); the Customers set already excludes soft-deleted rows.
        var genuineCustomers = _db.Customers.AsNoTracking()
            .Where(c => c.User != null && c.User.UserRoles.Any(ur => ur.Role!.Name == customerRole));

        var newRegistrations = await genuineCustomers
            .Where(c => c.CreatedOnUtc >= period.StartUtc && c.CreatedOnUtc < period.EndUtc)
            .CountAsync(cancellationToken);

        var totalActiveCustomers = await genuineCustomers.CountAsync(cancellationToken);

        // Top customers by lifetime (all-time) recognised order value.
        var topAgg = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId != null)
            .WithRecognisedRevenue()
            .GroupBy(o => o.CustomerId!.Value)
            .Select(g => new
            {
                CustomerId = g.Key,
                OrderCount = g.Count(),
                TotalSpent = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m,
            })
            .OrderByDescending(x => x.TotalSpent)
            .Take(topCount)
            .ToListAsync(cancellationToken);

        var ids = topAgg.Select(x => x.CustomerId).ToList();
        var profiles = await _db.Customers.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                Email = c.User != null ? c.User.Email : string.Empty,
            })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var topCustomers = topAgg.Select(x =>
        {
            profiles.TryGetValue(x.CustomerId, out var p);
            return new TopCustomerRowDto
            {
                CustomerId = x.CustomerId,
                CustomerName = p is null ? "(unknown)" : $"{p.FirstName} {p.LastName}".Trim(),
                Email = p?.Email ?? string.Empty,
                OrderCount = x.OrderCount,
                TotalSpent = x.TotalSpent,
            };
        }).ToList();

        return new CustomersReportDto
        {
            FromUtc = period.StartUtc,
            ToUtc = period.EndUtc,
            NewRegistrations = newRegistrations,
            TotalActiveCustomers = totalActiveCustomers,
            TopCustomers = topCustomers,
        };
    }
}
