using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// The authenticated customer's view of one of their own orders (WO-45 buyer view), including line
/// items. Orders that do not belong to the caller's customer profile are reported as not found so the
/// endpoint never leaks the existence of another customer's order.
/// </summary>
public record GetMyOrderQuery(Guid Id) : IRequest<OrderDto>;

public class GetMyOrderQueryHandler : IRequestHandler<GetMyOrderQuery, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetMyOrderQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<OrderDto> Handle(GetMyOrderQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.AssignedStore)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.Id);

        var dto = OrderDto.From(order);

        // Flag which line products are still viewable on the storefront (exist + published — the same gate
        // the storefront product page uses), so the buyer's order detail only links to ones that will load.
        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var publishedIds = (await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsPublished)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        foreach (var line in dto.Lines)
            line.ProductAvailable = publishedIds.Contains(line.ProductId);

        return dto;
    }
}
