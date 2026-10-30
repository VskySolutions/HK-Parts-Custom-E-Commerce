using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Reports;

/// <summary>
/// Produces a per-store performance report for a period: orders received, orders fulfilled, revenue and
/// average fulfilment time (AC-STR-005.1). Admins may report on any/all stores; a store manager is
/// restricted to their own store via <paramref name="ManagerScoped"/> (AC-STR-005.2).
/// </summary>
public record StorePerformanceReportQuery(
    Guid? StoreId,
    DateTime FromUtc,
    DateTime ToUtc,
    bool ManagerScoped = false) : IRequest<StorePerformanceReportDto>;

public class StorePerformanceReportQueryHandler : IRequestHandler<StorePerformanceReportQuery, StorePerformanceReportDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public StorePerformanceReportQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<StorePerformanceReportDto> Handle(StorePerformanceReportQuery request, CancellationToken cancellationToken)
    {
        // A store manager can only ever see their own store, regardless of any requested store id (AC-STR-005.2).
        Guid? storeFilter = request.StoreId;
        if (request.ManagerScoped)
        {
            var managerStoreId = await ResolveManagerStoreIdAsync(cancellationToken);
            if (request.StoreId is Guid requested && requested != managerStoreId)
                throw new ForbiddenAccessException("You can only report on your own store.");
            storeFilter = managerStoreId;
        }

        IQueryable<Order> query = _db.Orders
            .AsNoTracking()
            .Where(o => o.AssignedStoreId != null
                        && o.PlacedOnUtc >= request.FromUtc
                        && o.PlacedOnUtc < request.ToUtc)
            // Exclude cancelled/abandoned redirect-payment attempts so they don't inflate performance figures.
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries();

        if (storeFilter is Guid sid)
            query = query.Where(o => o.AssignedStoreId == sid);

        // Pull the minimal per-order fields for the window and aggregate in memory (avg of a Placed→Delivered
        // TimeSpan does not translate to SQL); a bounded date window keeps this cheap.
        var orders = await query
            .Select(o => new
            {
                StoreId = o.AssignedStoreId!.Value,
                o.Status,
                o.TotalAmount,
                o.PlacedOnUtc,
                o.DeliveredOnUtc,
            })
            .ToListAsync(cancellationToken);

        var rows = orders
            .GroupBy(o => o.StoreId)
            .Select(g =>
            {
                var delivered = g.Where(o => o.Status == OrderStatus.Delivered && o.DeliveredOnUtc.HasValue).ToList();
                double? avgHours = delivered.Count > 0
                    ? Math.Round(delivered.Average(o => (o.DeliveredOnUtc!.Value - o.PlacedOnUtc).TotalHours), 2)
                    : null;

                return new StorePerformanceRowDto
                {
                    StoreId = g.Key,
                    OrdersReceived = g.Count(),
                    OrdersFulfilled = delivered.Count,
                    Revenue = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount),
                    AverageFulfilmentHours = avgHours,
                };
            })
            .ToList();

        // Resolve store names, and ensure a scoped/requested store still appears (zeroed) when it had no orders.
        var storeIds = rows.Select(r => r.StoreId).ToList();
        if (storeFilter is Guid only && !storeIds.Contains(only))
            storeIds.Add(only);

        var names = await _db.Stores
            .AsNoTracking()
            .Where(s => storeIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        if (storeFilter is Guid scoped && rows.All(r => r.StoreId != scoped))
            rows.Add(new StorePerformanceRowDto { StoreId = scoped });

        foreach (var row in rows)
            row.StoreName = names.TryGetValue(row.StoreId, out var name) ? name : "(unknown store)";

        return new StorePerformanceReportDto
        {
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            Stores = rows.OrderBy(r => r.StoreName).ToList(),
        };
    }

    // (aggregation projected to an anonymous type above; no named row type needed)

    private async Task<Guid> ResolveManagerStoreIdAsync(CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new ForbiddenAccessException("You are not assigned to a store.");

        var storeId = await _db.StoreManagerAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.StoreId)
            .FirstOrDefaultAsync(cancellationToken);

        if (storeId == Guid.Empty)
            throw new ForbiddenAccessException("You are not assigned to a store.");

        return storeId;
    }
}
