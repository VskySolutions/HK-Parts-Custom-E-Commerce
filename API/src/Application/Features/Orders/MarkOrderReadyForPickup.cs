using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Marks a pickup order ready for collection (AC-SHP-004.3). Store-manager scoped: the order must be a
/// pickup order assigned to the caller's store. Notifies the buyer.
/// </summary>
public record MarkOrderReadyForPickupCommand(Guid OrderId) : IRequest<OrderDto>;

public class MarkOrderReadyForPickupCommandHandler : IRequestHandler<MarkOrderReadyForPickupCommand, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IEmailEnqueuer _emails;
    private readonly IDateTimeProvider _clock;

    public MarkOrderReadyForPickupCommandHandler(IApplicationDbContext db, ICurrentUserService current, IEmailEnqueuer emails, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _emails = emails;
        _clock = clock;
    }

    public async Task<OrderDto> Handle(MarkOrderReadyForPickupCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new ForbiddenAccessException("You are not assigned to a store.");

        var storeId = await _db.StoreManagerAssignments.AsNoTracking()
            .Where(a => a.UserId == userId).Select(a => a.StoreId).FirstOrDefaultAsync(cancellationToken);
        if (storeId == Guid.Empty)
            throw new ForbiddenAccessException("You are not assigned to a store.");

        var order = await _db.Orders.Include(o => o.Lines).Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.AssignedStoreId != storeId)
            throw new ForbiddenAccessException("This order is not assigned to your store.");
        if (!order.IsPickup)
            throw new ConflictException("This order is not a pickup-in-store order.");
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Delivered or OrderStatus.ReadyForPickup)
            throw new ConflictException($"An order in status '{order.Status}' cannot be marked ready for pickup.");

        var now = _clock.UtcNow;
        var from = order.Status;
        order.Status = OrderStatus.ReadyForPickup;
        _db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = from,
            ToStatus = OrderStatus.ReadyForPickup,
            ChangedById = userId,
            ChangedOnUtc = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(order.ContactEmail))
        {
            await _emails.EnqueueAsync(
                "OrderReadyForPickup",
                order.ContactEmail!,
                order.ContactName,
                $"Your order {order.OrderNumber} is ready for pickup",
                $"Hi {order.ContactName},\n\n" +
                $"Good news — your order {order.OrderNumber} is ready to collect in store.\n\n" +
                "Please bring your order number when you visit. Thank you for shopping with us.",
                cancellationToken: cancellationToken);
        }

        return OrderDto.From(order);
    }
}
