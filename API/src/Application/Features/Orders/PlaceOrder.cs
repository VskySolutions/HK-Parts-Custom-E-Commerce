using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>A requested line within a placed order.</summary>
public record PlaceOrderLine(Guid ProductId, Guid? ProductVariantId, int Quantity);

/// <summary>
/// Places an order: snapshots catalog pricing, runs the routing engine to pick a fulfilment store,
/// and reserves stock at the assigned store. Minimal order intake standing in for the future checkout
/// flow (WO-50); both guest (unauthenticated) and authenticated placement are supported.
/// </summary>
public record PlaceOrderCommand(
    string? ContactName,
    string? ContactEmail,
    double? Latitude,
    double? Longitude,
    string? CountryCode,
    string? Region,
    string? PostalCode,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateProvince,
    List<PlaceOrderLine> Items) : IRequest<OrderDto>;

public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(line =>
            line.RuleFor(l => l.Quantity).GreaterThan(0));
    }
}

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IOrderRoutingEngine _routing;
    private readonly IInventoryService _inventory;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;

    public PlaceOrderCommandHandler(
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

    public async Task<OrderDto> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. Snapshot each line from the catalog (name, SKU, unit price).
        var lines = new List<OrderLineItem>();
        foreach (var item in request.Items)
        {
            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken)
                ?? throw new NotFoundException(nameof(Product), item.ProductId);

            ProductVariant? variant = null;
            if (item.ProductVariantId is Guid variantId)
            {
                variant = await _db.ProductVariants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == product.Id, cancellationToken)
                    ?? throw new NotFoundException(nameof(ProductVariant), variantId);
            }

            var unitPrice = variant?.Price ?? product.Price ?? 0m;
            lines.Add(new OrderLineItem
            {
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                ProductName = product.Name,
                Sku = variant is not null ? variant.Sku ?? product.Sku : product.Sku,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                // No Customer Group pricing on this routing-only path: list price == charged price.
                OriginalUnitPrice = unitPrice,
                LineTotal = unitPrice * item.Quantity,
            });
        }

        // 2. Route the order against active stores.
        var routingItems = request.Items
            .Select(i => new RoutingLineItem(i.ProductId, i.ProductVariantId, i.Quantity))
            .ToList();
        var result = await _routing.RouteAsync(
            new RoutingRequest(request.Latitude, request.Longitude, request.CountryCode,
                request.Region, request.PostalCode, routingItems),
            cancellationToken);

        // 3. Create the order (resolve the customer only when the caller is an authenticated customer).
        var now = _clock.UtcNow;
        Guid? customerId = null;
        if (_current.UserId is Guid userId)
        {
            customerId = await _db.Customers
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var order = new Order
        {
            OrderNumber = $"ORD-{now:yyyyMMddHHmmssfff}",
            CustomerId = customerId,
            ShippingAddress = new Address
            {
                FirstName = request.ContactName,
                Email = request.ContactEmail,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                CountryCode = request.CountryCode ?? string.Empty,
                StateProvince = request.StateProvince ?? request.Region,
                PostalCode = request.PostalCode ?? string.Empty,
                AddressLine1 = request.AddressLine1 ?? string.Empty,
                AddressLine2 = request.AddressLine2,
                City = request.City ?? string.Empty,
            },
            PlacedOnUtc = now,
            TotalAmount = lines.Sum(l => l.LineTotal),
            Lines = lines,
        };

        // 4. Apply the routing outcome.
        if (result.IsRouted && result.AssignedStoreId is Guid storeId)
        {
            order.Status = OrderStatus.Routed;
            order.AssignedStoreId = storeId;
            order.RoutedOnUtc = now;

            // Best-effort reservation — routing already verified availability. Single stock-out path,
            // shared with the checkout orchestrator.
            await _inventory.DecrementForOrderAsync(order, cancellationToken);
        }
        else
        {
            order.Status = OrderStatus.Unrouted;
        }

        // 5. Persist and return the order plus its routing chain for visibility.
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = OrderDto.From(order);
        dto.RoutingChain = result.RoutingChain.ToList();
        return dto;
    }
}
