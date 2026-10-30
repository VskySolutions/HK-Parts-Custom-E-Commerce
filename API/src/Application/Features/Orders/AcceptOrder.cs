using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>Store manager accepts a routed order in their own store's queue (AC-STR-004.2).</summary>
public record AcceptOrderCommand(Guid OrderId) : IRequest<OrderDto>;

public class AcceptOrderCommandHandler : IRequestHandler<AcceptOrderCommand, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public AcceptOrderCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<OrderDto> Handle(AcceptOrderCommand request, CancellationToken cancellationToken)
    {
        var storeId = await ResolveManagerStoreIdAsync(cancellationToken);

        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.AssignedStoreId != storeId)
            throw new ForbiddenAccessException("This order is not assigned to your store.");

        if (order.Status != OrderStatus.Routed)
            throw new ConflictException($"An order in status '{order.Status}' cannot be accepted.");

        order.Status = OrderStatus.Accepted;
        await _db.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }

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
