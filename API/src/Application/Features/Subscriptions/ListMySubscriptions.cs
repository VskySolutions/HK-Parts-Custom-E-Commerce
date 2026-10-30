using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>
/// The authenticated buyer's own subscriptions (AC-ORD-005.4), newest first. Scoped strictly to the
/// caller's customer profile — a buyer can never see another customer's subscriptions.
/// </summary>
public record ListMySubscriptionsQuery(int Page = 1, int PageSize = 20) : IRequest<PaginatedList<SubscriptionDto>>;

public class ListMySubscriptionsQueryHandler : IRequestHandler<ListMySubscriptionsQuery, PaginatedList<SubscriptionDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public ListMySubscriptionsQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PaginatedList<SubscriptionDto>> Handle(ListMySubscriptionsQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var ordered = _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Customer)
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.CreatedOnUtc)
            .ThenByDescending(s => s.Id);

        var page = await PaginatedList<Subscription>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(SubscriptionDto.From).ToList();
        return new PaginatedList<SubscriptionDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
