using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// The authenticated customer's own order status timeline (WO-45 buyer view), oldest transition first —
/// the buyer-facing counterpart to <see cref="GetOrderStatusHistoryQuery"/>. Orders that do not belong to
/// the caller's customer profile are reported as not found so the endpoint never leaks another customer's
/// order. Reuses <see cref="OrderStatusHistoryDto"/>.
/// </summary>
public record GetMyOrderTimelineQuery(Guid OrderId) : IRequest<List<OrderStatusHistoryDto>>;

public class GetMyOrderTimelineQueryHandler : IRequestHandler<GetMyOrderTimelineQuery, List<OrderStatusHistoryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetMyOrderTimelineQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<OrderStatusHistoryDto>> Handle(GetMyOrderTimelineQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        // Ownership check: not found unless the order belongs to the signed-in customer.
        var owned = await _db.Orders
            .AsNoTracking()
            .AnyAsync(o => o.Id == request.OrderId && o.CustomerId == customerId, cancellationToken);
        if (!owned)
            throw new NotFoundException(nameof(Order), request.OrderId);

        var history = await _db.OrderStatusHistory
            .AsNoTracking()
            .Where(h => h.OrderId == request.OrderId)
            .OrderBy(h => h.ChangedOnUtc)
            .ToListAsync(cancellationToken);

        return history.Select(OrderStatusHistoryDto.From).ToList();
    }
}
