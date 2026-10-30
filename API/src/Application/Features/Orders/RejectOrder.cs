using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Store manager rejects an order in their store. Restores the stock reserved at that store, records
/// the store as excluded, then re-runs routing to the next eligible store — landing the order back in
/// another store's queue, or Unrouted when none remain (AC-STR-004.2, AC-STR-003.4).
/// </summary>
public record RejectOrderCommand(Guid OrderId) : IRequest<OrderDto>;

public class RejectOrderCommandHandler : IRequestHandler<RejectOrderCommand, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IOrderRoutingEngine _routing;
    private readonly IInventoryService _inventory;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;

    public RejectOrderCommandHandler(
        IApplicationDbContext db,
        IOrderRoutingEngine routing,
        IInventoryService inventory,
        ICurrentUserService current,
        IDateTimeProvider clock)
    {
        _db = db;
        _routing = routing;
        _inventory = inventory;
        _current = current;
        _clock = clock;
    }

    public async Task<OrderDto> Handle(RejectOrderCommand request, CancellationToken cancellationToken)
    {
        var storeId = await ResolveManagerStoreIdAsync(cancellationToken);

        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.AssignedStoreId != storeId)
            throw new ForbiddenAccessException("This order is not assigned to your store.");

        if (order.Status is not (OrderStatus.Routed or OrderStatus.Accepted))
            throw new ConflictException($"An order in status '{order.Status}' cannot be rejected.");

        // Release the stock reserved at the rejecting store.
        foreach (var line in order.Lines)
            await _inventory.RestoreStockAsync(line.ProductId, line.ProductVariantId, storeId, line.Quantity, cancellationToken);

        // Accumulate the excluded set so routing skips every store already tried.
        var excluded = ParseExcluded(order.ExcludedStoreIdsJson);
        if (!excluded.Contains(storeId))
            excluded.Add(storeId);
        order.ExcludedStoreIdsJson = JsonSerializer.Serialize(excluded);

        var routingItems = order.Lines
            .Select(l => new RoutingLineItem(l.ProductId, l.ProductVariantId, l.Quantity))
            .ToList();
        var result = await _routing.RouteAsync(
            new RoutingRequest(order.Latitude, order.Longitude, order.CountryCode,
                order.Region, order.PostalCode, routingItems, excluded),
            cancellationToken);

        var now = _clock.UtcNow;
        if (result.IsRouted && result.AssignedStoreId is Guid nextStoreId)
        {
            order.AssignedStoreId = nextStoreId;
            order.Status = OrderStatus.Routed;
            order.RoutedOnUtc = now;
            foreach (var line in order.Lines)
                await _inventory.DecrementStockAsync(line.ProductId, line.ProductVariantId, nextStoreId, line.Quantity, cancellationToken);
        }
        else
        {
            order.Status = OrderStatus.Unrouted;
            order.AssignedStoreId = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = OrderDto.From(order);
        dto.RoutingChain = result.RoutingChain.ToList();
        return dto;
    }

    private static List<Guid> ParseExcluded(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new List<Guid>()
            : JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>();

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
