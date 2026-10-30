using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Store manager advances one of their store's orders through fulfilment. Only Preparing, Shipped and
/// Delivered are valid targets here; any other target is rejected with a conflict (AC-STR-004.3).
/// </summary>
public record UpdateFulfilmentStatusCommand(Guid OrderId, string Status) : IRequest<OrderDto>;

public class UpdateFulfilmentStatusCommandHandler : IRequestHandler<UpdateFulfilmentStatusCommand, OrderDto>
{
    private static readonly OrderStatus[] Allowed =
        { OrderStatus.Preparing, OrderStatus.Shipped, OrderStatus.Delivered };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public UpdateFulfilmentStatusCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<OrderDto> Handle(UpdateFulfilmentStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var target) || !Allowed.Contains(target))
            throw new ConflictException(
                $"'{request.Status}' is not a valid fulfilment status. Allowed: Preparing, Shipped, Delivered.");

        var storeId = await ResolveManagerStoreIdAsync(cancellationToken);

        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.AssignedStoreId != storeId)
            throw new ForbiddenAccessException("This order is not assigned to your store.");

        order.Status = target;
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
