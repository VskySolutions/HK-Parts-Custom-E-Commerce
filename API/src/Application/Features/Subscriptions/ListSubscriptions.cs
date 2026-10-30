using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>
/// Admin view of all subscriptions across customers (paged), optionally filtered by customer and status,
/// soonest next-order first. Includes customer + product names for the management grid (WO-49 admin note).
/// </summary>
public record ListSubscriptionsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? CustomerId = null,
    SubscriptionStatus? Status = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<SubscriptionDto>>;

public class ListSubscriptionsQueryHandler : IRequestHandler<ListSubscriptionsQuery, PaginatedList<SubscriptionDto>>
{
    // Grid column name -> entity property path. Anything else falls back to NextOrderOnUtc ascending.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["nextOrderOnUtc"] = "NextOrderOnUtc",
        ["lastOrderOnUtc"] = "LastOrderOnUtc",
        ["status"] = "Status",
        ["interval"] = "Interval",
        ["quantity"] = "Quantity",
        ["createdOnUtc"] = "CreatedOnUtc",
    };

    private readonly IApplicationDbContext _db;

    public ListSubscriptionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<SubscriptionDto>> Handle(ListSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Subscription> query = _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Customer);

        if (request.CustomerId is Guid customerId)
            query = query.Where(s => s.CustomerId == customerId);

        if (request.Status is SubscriptionStatus status)
            query = query.Where(s => s.Status == status);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(s => s.NextOrderOnUtc));

        var page = await PaginatedList<Subscription>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(SubscriptionDto.From).ToList();
        return new PaginatedList<SubscriptionDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
