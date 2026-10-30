using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Admin list of all orders, newest first, optionally filtered by status name (e.g. "Unrouted" to
/// surface orders that no store could fulfil) and/or an order-number / contact search term.
/// </summary>
public record ListOrdersQuery(string? Status, int Page = 1, int PageSize = 20, string? Search = null,
    string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<OrderSummaryDto>>;

public class ListOrdersQueryHandler : IRequestHandler<ListOrdersQuery, PaginatedList<OrderSummaryDto>>
{
    // Column name (from the grid) -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["orderNumber"] = "OrderNumber",
        ["status"] = "Status",
        ["placedOnUtc"] = "PlacedOnUtc",
        ["totalAmount"] = "TotalAmount",
    };

    private readonly IApplicationDbContext _db;

    public ListOrdersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<OrderSummaryDto>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Order> query = _db.Orders.Include(o => o.ShippingAddress)
            .AsNoTracking()
            .Include(o => o.Lines)
            // Hide orders still awaiting an off-site redirect payment (cancelled/abandoned Stripe attempts).
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            // Search the linked Address directly: Order.ContactName and ContactEmail are [NotMapped]
            // read-throughs over it, so EF cannot translate them and the query throws at runtime.
            query = query.Where(o => o.OrderNumber.Contains(term)
                || (o.ShippingAddress != null &&
                    ((o.ShippingAddress.FirstName != null && o.ShippingAddress.FirstName.Contains(term))
                     || (o.ShippingAddress.LastName != null && o.ShippingAddress.LastName.Contains(term))
                     || (o.ShippingAddress.Email != null && o.ShippingAddress.Email.Contains(term)))));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderByDescending(o => o.PlacedOnUtc));
        var page = await PaginatedList<Order>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(OrderSummaryDto.From).ToList();
        return new PaginatedList<OrderSummaryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
