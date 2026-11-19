using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// One guest buyer: everyone who placed a storefront order without an account (Order.CustomerId is null),
/// grouped by the order's contact email. Name / phone / location come from their most recent order.
/// </summary>
public class GuestCustomerDto
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? CountryCode { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime FirstOrderUtc { get; set; }
    public DateTime LastOrderUtc { get; set; }
    public string? LastOrderNumber { get; set; }

    /// <summary>The account id when this email has since registered (their guest orders stay unlinked).</summary>
    public Guid? RegisteredCustomerId { get; set; }
}

/// <summary>A guest's orders (newest first) for the Guest Customers drill-down.</summary>
public class GuestOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime PlacedOnUtc { get; set; }
}

internal static class GuestOrders
{
    /// <summary>
    /// Storefront orders placed without an account: no customer, not an eBay import, not an inquiry, and not an
    /// abandoned off-site payment attempt (same exclusions as the admin Orders list).
    /// </summary>
    public static IQueryable<Order> Query(IApplicationDbContext db) =>
        db.Orders.AsNoTracking()
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries()
            .Where(o => o.CustomerId == null
                        && o.EbayOrderId == null
                        && o.ShippingAddress != null
                        && o.ShippingAddress.Email != null
                        && o.ShippingAddress.Email != "");
}

public record ListGuestCustomersQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<PaginatedList<GuestCustomerDto>>;

public class ListGuestCustomersQueryHandler : IRequestHandler<ListGuestCustomersQuery, PaginatedList<GuestCustomerDto>>
{
    private readonly IApplicationDbContext _db;

    public ListGuestCustomersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<GuestCustomerDto>> Handle(ListGuestCustomersQuery request, CancellationToken ct)
    {
        var orders = GuestOrders.Query(_db);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            orders = orders.Where(o => o.OrderNumber.Contains(term)
                                       || o.ShippingAddress!.Email!.Contains(term)
                                       || o.ShippingAddress.FirstName.Contains(term)
                                       || o.ShippingAddress.LastName.Contains(term)
                                       || (o.ShippingAddress.PhoneNumber != null && o.ShippingAddress.PhoneNumber.Contains(term)));
        }

        // One row per email (the default SQL collation is case-insensitive, so casing variants group together).
        var grouped = orders
            .GroupBy(o => o.ShippingAddress!.Email!)
            .Select(g => new GuestAggregate
            {
                Email = g.Key,
                OrderCount = g.Count(),
                TotalSpent = g.Sum(o => o.TotalAmount),
                FirstOrderUtc = g.Min(o => o.PlacedOnUtc),
                LastOrderUtc = g.Max(o => o.PlacedOnUtc),
            })
            .OrderByDescending(x => x.LastOrderUtc);

        var page = await PaginatedList<GuestAggregate>.CreateAsync(
            grouped, request.Page, Math.Clamp(request.PageSize, 1, 200), ct);
        var emails = page.Items.Select(x => x.Email).ToList();

        // Contact details from each guest's most recent order.
        var latest = await GuestOrders.Query(_db)
            .Where(o => emails.Contains(o.ShippingAddress!.Email!))
            .OrderByDescending(o => o.PlacedOnUtc)
            .Select(o => new
            {
                Email = o.ShippingAddress!.Email!,
                o.ShippingAddress.FirstName,
                o.ShippingAddress.LastName,
                o.ShippingAddress.PhoneNumber,
                o.ShippingAddress.City,
                o.ShippingAddress.StateProvince,
                o.ShippingAddress.CountryCode,
                o.OrderNumber,
                o.CurrencyCode,
            })
            .ToListAsync(ct);
        var latestByEmail = latest
            .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Guests who have since created an account.
        var registered = await _db.Customers.AsNoTracking()
            .Where(c => c.User != null && emails.Contains(c.User.Email))
            .Select(c => new { c.Id, c.User!.Email })
            .ToListAsync(ct);
        var registeredByEmail = registered
            .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var items = page.Items.Select(x =>
        {
            var email = x.Email;
            latestByEmail.TryGetValue(email, out var last);
            return new GuestCustomerDto
            {
                Email = email,
                FirstName = last?.FirstName,
                LastName = last?.LastName,
                PhoneNumber = last?.PhoneNumber,
                City = last?.City,
                StateProvince = last?.StateProvince,
                CountryCode = last?.CountryCode,
                LastOrderNumber = last?.OrderNumber,
                CurrencyCode = last?.CurrencyCode ?? "USD",
                OrderCount = x.OrderCount,
                TotalSpent = x.TotalSpent,
                FirstOrderUtc = x.FirstOrderUtc,
                LastOrderUtc = x.LastOrderUtc,
                RegisteredCustomerId = registeredByEmail.TryGetValue(email, out var id) ? id : null,
            };
        }).ToList();

        return new PaginatedList<GuestCustomerDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    public class GuestAggregate
    {
        public string Email { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime FirstOrderUtc { get; set; }
        public DateTime LastOrderUtc { get; set; }
    }
}

public record ListGuestCustomerOrdersQuery(string Email) : IRequest<List<GuestOrderDto>>;

public class ListGuestCustomerOrdersQueryHandler : IRequestHandler<ListGuestCustomerOrdersQuery, List<GuestOrderDto>>
{
    private readonly IApplicationDbContext _db;

    public ListGuestCustomerOrdersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<GuestOrderDto>> Handle(ListGuestCustomerOrdersQuery request, CancellationToken ct)
    {
        var email = (request.Email ?? string.Empty).Trim();
        if (email.Length == 0)
            return new List<GuestOrderDto>();

        return await GuestOrders.Query(_db)
            .Where(o => o.ShippingAddress!.Email == email)
            .OrderByDescending(o => o.PlacedOnUtc)
            .Select(o => new GuestOrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                Status = o.Status.ToString(),
                PaymentStatus = o.PaymentStatus.ToString(),
                TotalAmount = o.TotalAmount,
                CurrencyCode = o.CurrencyCode,
                PlacedOnUtc = o.PlacedOnUtc,
            })
            .ToListAsync(ct);
    }
}
