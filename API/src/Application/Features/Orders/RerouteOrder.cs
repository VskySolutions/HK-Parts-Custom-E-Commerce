using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Admin manual routing override (AC-STR-003.6). With a target store, the order is reassigned directly
/// to it; with no target, the routing engine is re-run fresh (no exclusions). Either way any stock
/// reserved at the previously-assigned store is released and re-reserved at the new store.
/// </summary>
public record RerouteOrderCommand(Guid OrderId, Guid? TargetStoreId) : IRequest<OrderDto>;

public class RerouteOrderCommandHandler : IRequestHandler<RerouteOrderCommand, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IOrderRoutingEngine _routing;
    private readonly IInventoryService _inventory;
    private readonly IDateTimeProvider _clock;

    public RerouteOrderCommandHandler(
        IApplicationDbContext db,
        IOrderRoutingEngine routing,
        IInventoryService inventory,
        IDateTimeProvider clock)
    {
        _db = db;
        _routing = routing;
        _inventory = inventory;
        _clock = clock;
    }

    public async Task<OrderDto> Handle(RerouteOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        // Release stock held at the currently-assigned store before re-assigning.
        if (order.AssignedStoreId is Guid oldStoreId)
        {
            foreach (var line in order.Lines)
                await _inventory.RestoreStockAsync(line.ProductId, line.ProductVariantId, oldStoreId, line.Quantity, cancellationToken);
        }

        RoutingResult? result = null;
        Guid? targetStoreId;

        if (request.TargetStoreId is Guid target)
        {
            var storeExists = await _db.Stores.AsNoTracking().AnyAsync(s => s.Id == target, cancellationToken);
            if (!storeExists)
                throw new NotFoundException(nameof(Store), target);
            targetStoreId = target;
        }
        else
        {
            var routingItems = order.Lines
                .Select(l => new RoutingLineItem(l.ProductId, l.ProductVariantId, l.Quantity))
                .ToList();
            result = await _routing.RouteAsync(
                new RoutingRequest(order.Latitude, order.Longitude, order.CountryCode,
                    order.Region, order.PostalCode, routingItems),
                cancellationToken);
            targetStoreId = result.IsRouted ? result.AssignedStoreId : null;
        }

        var now = _clock.UtcNow;
        if (targetStoreId is Guid assignedStoreId)
        {
            order.AssignedStoreId = assignedStoreId;
            order.Status = OrderStatus.Routed;
            order.RoutedOnUtc = now;
            foreach (var line in order.Lines)
                await _inventory.DecrementStockAsync(line.ProductId, line.ProductVariantId, assignedStoreId, line.Quantity, cancellationToken);
        }
        else
        {
            order.Status = OrderStatus.Unrouted;
            order.AssignedStoreId = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = OrderDto.From(order);
        if (result is not null)
            dto.RoutingChain = result.RoutingChain.ToList();
        return dto;
    }
}
