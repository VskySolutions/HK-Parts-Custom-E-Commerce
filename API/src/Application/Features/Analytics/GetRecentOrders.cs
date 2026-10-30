using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Analytics;

/// <summary>A recent-order row for the dashboard's recent-orders table (AC-ADM-001.3).</summary>
public class RecentOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime PlacedOnUtc { get; set; }
    public string? CustomerName { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

/// <summary>
/// The most recently placed orders for the dashboard (AC-ADM-001.3), newest first. Provisional (abandoned
/// redirect-payment) shells are excluded, but all real orders are shown regardless of payment state so admins
/// see current activity. The customer name comes from the linked customer profile, falling back to the
/// shipping-address contact name for guest orders.
/// </summary>
public record GetRecentOrdersQuery(int Take = 10) : IRequest<IReadOnlyList<RecentOrderDto>>;

public class GetRecentOrdersQueryHandler : IRequestHandler<GetRecentOrdersQuery, IReadOnlyList<RecentOrderDto>>
{
    private readonly IApplicationDbContext _db;

    public GetRecentOrdersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<RecentOrderDto>> Handle(GetRecentOrdersQuery request, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 50);

        // Name parts are pulled separately (Order's contact fields are [NotMapped] read-throughs and can't be
        // projected in SQL) then combined in memory.
        var rows = await _db.Orders.AsNoTracking()
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries()
            .OrderByDescending(o => o.PlacedOnUtc)
            .Take(take)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.PlacedOnUtc,
                o.Status,
                o.TotalAmount,
                o.CurrencyCode,
                CustomerFirst = o.Customer != null ? o.Customer.FirstName : null,
                CustomerLast = o.Customer != null ? o.Customer.LastName : null,
                GuestFirst = o.ShippingAddress != null ? o.ShippingAddress.FirstName : null,
                GuestLast = o.ShippingAddress != null ? o.ShippingAddress.LastName : null,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(o => new RecentOrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            PlacedOnUtc = o.PlacedOnUtc,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
            CurrencyCode = o.CurrencyCode,
            CustomerName = CombineName(o.CustomerFirst, o.CustomerLast) ?? CombineName(o.GuestFirst, o.GuestLast),
        }).ToList();
    }

    private static string? CombineName(string? first, string? last)
    {
        var name = $"{first} {last}".Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
