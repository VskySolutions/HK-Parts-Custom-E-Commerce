using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.OrderRouting;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Routing;

/// <summary>
/// Order routing engine (WO-51). Evaluates all active, non-maintenance stores against delivery-zone,
/// stock, and capacity capability checks, then selects the nearest eligible store by great-circle
/// (haversine) distance. Supports a fallback chain via <see cref="RoutingRequest.ExcludeStoreIds"/>
/// and raises an admin alert + <see cref="OrderUnroutable"/> event when nothing is eligible.
/// </summary>
public class OrderRoutingEngine : IOrderRoutingEngine
{
    private readonly IApplicationDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IAdminAlertService _alerts;
    private readonly IPublisher _publisher;

    public OrderRoutingEngine(
        IApplicationDbContext db,
        IInventoryService inventory,
        IAdminAlertService alerts,
        IPublisher publisher)
    {
        _db = db;
        _inventory = inventory;
        _alerts = alerts;
        _publisher = publisher;
    }

    public async Task<RoutingResult> RouteAsync(RoutingRequest request, CancellationToken cancellationToken = default)
    {
        var exclude = request.ExcludeStoreIds is { Count: > 0 }
            ? new HashSet<Guid>(request.ExcludeStoreIds)
            : new HashSet<Guid>();

        // Soft-deleted stores are removed by the global query filter; also require active + not in maintenance.
        var stores = await _db.Stores
            .AsNoTracking()
            .Include(s => s.DeliveryZones)
            .Include(s => s.Address)
            .Where(s => s.IsEnabled && !s.MaintenanceMode)
            .ToListAsync(cancellationToken);

        // Active-order counts per store, for the capacity check (AC-STR-003.1).
        var activeStatuses = new[] { OrderStatus.Routed, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Shipped };
        var activeOrderCounts = await _db.Orders
            .AsNoTracking()
            .Where(o => o.AssignedStoreId != null && activeStatuses.Contains(o.Status))
            .GroupBy(o => o.AssignedStoreId!.Value)
            .Select(g => new { StoreId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.StoreId, g => g.Count, cancellationToken);

        var evaluations = new List<StoreEvaluation>();
        var eligible = new List<(Store Store, double? Distance)>();

        foreach (var store in stores)
        {
            var distance = Distance(store, request);

            if (exclude.Contains(store.Id))
            {
                evaluations.Add(new StoreEvaluation(store.Id, store.Name, false, "Excluded by a previous fallback attempt", distance));
                continue;
            }

            if (!ServesAddress(store, request))
            {
                evaluations.Add(new StoreEvaluation(store.Id, store.Name, false, "Delivery address is outside the store's delivery zones", distance));
                continue;
            }

            // Capacity (AC-STR-003.1): reject stores at or above their active-order limit.
            if (store.OrderCapacityLimit is int capacity && capacity > 0)
            {
                var currentLoad = activeOrderCounts.TryGetValue(store.Id, out var c) ? c : 0;
                if (currentLoad >= capacity)
                {
                    evaluations.Add(new StoreEvaluation(store.Id, store.Name, false, "Store has reached its order capacity limit", distance));
                    continue;
                }
            }

            var stockOk = true;
            foreach (var item in request.Items)
            {
                if (!await _inventory.IsAvailableAsync(item.ProductId, item.ProductVariantId, store.Id, item.Quantity, cancellationToken))
                {
                    stockOk = false;
                    break;
                }
            }

            if (!stockOk)
            {
                evaluations.Add(new StoreEvaluation(store.Id, store.Name, false, "Insufficient stock for one or more line items", distance));
                continue;
            }

            eligible.Add((store, distance));
            evaluations.Add(new StoreEvaluation(store.Id, store.Name, true, null, distance));
        }

        // Nearest eligible: known distances first (ascending), unknown-distance stores last.
        var chosen = eligible
            .OrderBy(e => e.Distance.HasValue ? 0 : 1)
            .ThenBy(e => e.Distance ?? double.MaxValue)
            .Select(e => e.Store)
            .FirstOrDefault();

        // Present the chain eligible-first, then by proximity, for transparency and fallback.
        var chain = evaluations
            .OrderByDescending(e => e.Eligible)
            .ThenBy(e => e.DistanceKm.HasValue ? 0 : 1)
            .ThenBy(e => e.DistanceKm ?? double.MaxValue)
            .ToList();

        if (chosen is null)
        {
            await _alerts.RaiseAsync(
                "OrderUnroutable",
                "Order could not be routed to any store",
                "No active store satisfied the stock, delivery-zone, and capacity checks for this order.",
                "Error",
                "OrderRoutingEngine",
                cancellationToken);
            await _publisher.Publish(new OrderUnroutable(request), cancellationToken);
            return new RoutingResult(false, null, null, chain);
        }

        await _publisher.Publish(new OrderRouted(request, chosen.Id), cancellationToken);
        return new RoutingResult(true, chosen.Id, ComposeAddress(chosen), chain);
    }

    /// <summary>True when the buyer address falls within one of the store's active delivery zones.
    /// A store with no configured zones is treated as unrestricted.</summary>
    private static bool ServesAddress(Store store, RoutingRequest request)
    {
        var zones = store.DeliveryZones.Where(z => z.IsActive).ToList();
        if (zones.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(request.CountryCode))
            return false;

        foreach (var zone in zones)
        {
            if (!string.Equals(zone.CountryCode, request.CountryCode, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(zone.Region) &&
                !string.Equals(zone.Region, request.Region, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(zone.PostalCodeStart) &&
                !string.IsNullOrWhiteSpace(zone.PostalCodeEnd) &&
                !string.IsNullOrWhiteSpace(request.PostalCode))
            {
                if (string.Compare(request.PostalCode, zone.PostalCodeStart, StringComparison.OrdinalIgnoreCase) < 0 ||
                    string.Compare(request.PostalCode, zone.PostalCodeEnd, StringComparison.OrdinalIgnoreCase) > 0)
                    continue;
            }

            return true;
        }

        return false;
    }

    private static double? Distance(Store store, RoutingRequest request)
    {
        if (store.Latitude is null || store.Longitude is null || request.Latitude is null || request.Longitude is null)
            return null;
        return Haversine(request.Latitude.Value, request.Longitude.Value, store.Latitude.Value, store.Longitude.Value);
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static string ComposeAddress(Store s) =>
        string.Join(", ", new[] { s.AddressLine1, s.AddressLine2, s.City, s.StateProvince, s.PostalCode, s.CountryCode }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
}
