using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Inventory;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Inventory;

/// <summary>
/// Per-store inventory tracking for products and variants (REQ-CAT-011). The single write path for
/// stock: the order service calls <see cref="DecrementStockAsync"/> at confirmation,
/// <see cref="RestoreStockAsync"/> on pre-shipment cancellation and <see cref="MarkAsReceivedAsync"/>
/// on RMA acceptance, while admins/store managers use the set/adjust methods. Low-stock alerts are
/// raised via <see cref="IAdminAlertService"/> when a level falls to or below its threshold
/// (AC-CAT-011.2), guarded so only one alert fires per depletion cycle.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly IApplicationDbContext _db;
    private readonly IAdminAlertService _alerts;
    private readonly IPublisher _publisher;

    public InventoryService(IApplicationDbContext db, IAdminAlertService alerts, IPublisher publisher)
    {
        _db = db;
        _alerts = alerts;
        _publisher = publisher;
    }

    public async Task<bool> DecrementStockAsync(Guid productId, Guid? variantId, Guid storeId, int quantity, CancellationToken ct = default)
    {
        var level = await FindLevelAsync(productId, variantId, storeId, ct);

        if (level is not null && level.StockQuantity - level.ReservedQuantity >= quantity)
        {
            level.StockQuantity -= quantity;
            await _db.SaveChangesAsync(ct);
            await CheckLowStockAsync(level, ct);
            return true;
        }

        // Insufficient available stock: only proceed when the product/variant permits backorders (AC-CAT-011.3).
        if (!await AllowsBackorderAsync(productId, variantId, ct))
            return false;

        if (level is not null)
        {
            level.StockQuantity -= quantity; // allowed to go negative to represent the backorder
            await _db.SaveChangesAsync(ct);
            await CheckLowStockAsync(level, ct);
        }

        return true;
    }

    public async Task RestoreStockAsync(Guid productId, Guid? variantId, Guid storeId, int quantity, CancellationToken ct = default)
    {
        var level = await FindLevelAsync(productId, variantId, storeId, ct);
        if (level is null)
            return;

        var previous = level.StockQuantity;
        level.StockQuantity += quantity;
        ClearAlertIfReplenished(level);
        await _db.SaveChangesAsync(ct);
        await PublishReplenishedIfCrossedAsync(level, previous, quantity, ct);
    }

    public async Task DecrementForOrderAsync(Order order, CancellationToken ct = default)
    {
        // The order was routed/fulfilled by exactly one store; stock is committed there. Nothing to do for
        // an unrouted order (it never held stock).
        if (order.AssignedStoreId is not Guid storeId)
            return;

        foreach (var line in order.Lines)
            await DecrementStockAsync(line.ProductId, line.ProductVariantId, storeId, line.Quantity, ct);
    }

    public async Task RestoreForOrderAsync(Order order, IReadOnlyCollection<Guid>? orderLineItemIds = null, CancellationToken ct = default)
    {
        if (order.AssignedStoreId is not Guid storeId)
            return;

        // All lines by default; a line-item refund restores only the returned lines.
        var lines = orderLineItemIds is { Count: > 0 }
            ? order.Lines.Where(l => orderLineItemIds.Contains(l.Id))
            : order.Lines;

        foreach (var line in lines)
            await RestoreStockAsync(line.ProductId, line.ProductVariantId, storeId, line.Quantity, ct);
    }

    public async Task MarkAsReceivedAsync(Guid productId, Guid? variantId, Guid storeId, int quantityAccepted, CancellationToken ct = default)
    {
        var level = await FindLevelAsync(productId, variantId, storeId, ct);
        if (level is null)
            return;

        var previous = level.StockQuantity;
        level.StockQuantity += quantityAccepted;
        ClearAlertIfReplenished(level);
        await _db.SaveChangesAsync(ct);
        await PublishReplenishedIfCrossedAsync(level, previous, quantityAccepted, ct);
    }

    public async Task<int> SetStockLevelAsync(Guid productId, Guid? variantId, Guid storeId, int quantity, int? lowStockThreshold, CancellationToken ct = default)
    {
        var level = await FindLevelAsync(productId, variantId, storeId, ct);
        var existedBefore = level is not null;
        var previous = level?.StockQuantity ?? 0;
        if (level is null)
        {
            await EnsureProductAndStoreExistAsync(productId, variantId, storeId, ct);
            level = new InventoryLevel
            {
                ProductId = productId,
                ProductVariantId = variantId,
                StoreId = storeId,
            };
            _db.InventoryLevels.Add(level);
        }

        level.StockQuantity = quantity;
        if (lowStockThreshold.HasValue)
            level.LowStockThreshold = lowStockThreshold.Value;

        ClearAlertIfReplenished(level);
        await _db.SaveChangesAsync(ct);
        await CheckLowStockAsync(level, ct);
        if (existedBefore)
            await PublishReplenishedIfCrossedAsync(level, previous, level.StockQuantity - previous, ct);
        return level.StockQuantity;
    }

    public async Task<int> AdjustStockAsync(Guid productId, Guid? variantId, Guid storeId, int delta, CancellationToken ct = default)
    {
        var level = await FindLevelAsync(productId, variantId, storeId, ct);
        var existedBefore = level is not null;
        var previous = level?.StockQuantity ?? 0;
        if (level is null)
        {
            await EnsureProductAndStoreExistAsync(productId, variantId, storeId, ct);
            level = new InventoryLevel
            {
                ProductId = productId,
                ProductVariantId = variantId,
                StoreId = storeId,
            };
            _db.InventoryLevels.Add(level);
        }

        level.StockQuantity += delta;
        ClearAlertIfReplenished(level);
        await _db.SaveChangesAsync(ct);
        await CheckLowStockAsync(level, ct);
        if (existedBefore)
            await PublishReplenishedIfCrossedAsync(level, previous, delta, ct);
        return level.StockQuantity;
    }

    public async Task<bool> IsAvailableAsync(Guid productId, Guid? variantId, Guid storeId, int quantity, CancellationToken ct = default)
    {
        var level = await _db.InventoryLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.ProductId == productId && l.ProductVariantId == variantId && l.StoreId == storeId, ct);

        var available = (level?.StockQuantity ?? 0) - (level?.ReservedQuantity ?? 0);
        if (available >= quantity)
            return true;

        return await AllowsBackorderAsync(productId, variantId, ct);
    }

    private Task<InventoryLevel?> FindLevelAsync(Guid productId, Guid? variantId, Guid storeId, CancellationToken ct)
        => _db.InventoryLevels.FirstOrDefaultAsync(
            l => l.ProductId == productId && l.ProductVariantId == variantId && l.StoreId == storeId, ct);

    /// <summary>
    /// Backorder policy for the target: the variant's own flag when a variant is tracked
    /// (AC-CAT-002.4), otherwise the product's flag (AC-CAT-011.3).
    /// </summary>
    private async Task<bool> AllowsBackorderAsync(Guid productId, Guid? variantId, CancellationToken ct)
    {
        if (variantId is Guid vid)
        {
            var variant = await _db.ProductVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vid, ct);
            if (variant is not null)
                return variant.AllowBackorder;
        }

        return await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.AllowBackorder)
            .FirstOrDefaultAsync(ct);
    }

    private async Task EnsureProductAndStoreExistAsync(Guid productId, Guid? variantId, Guid storeId, CancellationToken ct)
    {
        if (!await _db.Products.AsNoTracking().AnyAsync(p => p.Id == productId, ct))
            throw new NotFoundException(nameof(Product), productId);

        if (variantId is Guid vid && !await _db.ProductVariants.AsNoTracking().AnyAsync(v => v.Id == vid, ct))
            throw new NotFoundException(nameof(ProductVariant), vid);

        if (!await _db.Stores.AsNoTracking().AnyAsync(s => s.Id == storeId, ct))
            throw new NotFoundException(nameof(Store), storeId);
    }

    /// <summary>Raises a single low-stock alert per depletion cycle (AC-CAT-011.2).</summary>
    private async Task CheckLowStockAsync(InventoryLevel level, CancellationToken ct)
    {
        if (level.LowStockThreshold > 0 && level.StockQuantity <= level.LowStockThreshold && !level.LowStockAlerted)
        {
            var variantPart = level.ProductVariantId is Guid v ? $" (variant {v})" : string.Empty;
            var title = $"Low stock: product {level.ProductId} at store {level.StoreId}";
            var message = $"Stock for product {level.ProductId}{variantPart} at store {level.StoreId} is " +
                          $"{level.StockQuantity}, at or below the low-stock threshold of {level.LowStockThreshold}.";

            await _alerts.RaiseAsync("LowStock", title, message, "Warning", "InventoryService", ct);
            level.LowStockAlerted = true;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Publishes <see cref="InventoryReplenished"/> when a level crosses from ≤0 (backordered/out of stock)
    /// back to positive, so the backorder fulfilment worker can release waiting orders (AC-CAT-013.4).
    /// </summary>
    private async Task PublishReplenishedIfCrossedAsync(InventoryLevel level, int previousQty, int quantityAdded, CancellationToken ct)
    {
        if (previousQty <= 0 && level.StockQuantity > 0 && quantityAdded > 0)
            await _publisher.Publish(
                new InventoryReplenished(level.ProductId, level.ProductVariantId, level.StoreId, quantityAdded), ct);
    }

    /// <summary>Clears the alert guard once stock rises back above the threshold so alerting can re-arm.</summary>
    private static void ClearAlertIfReplenished(InventoryLevel level)
    {
        if (level.LowStockAlerted && (level.LowStockThreshold <= 0 || level.StockQuantity > level.LowStockThreshold))
            level.LowStockAlerted = false;
    }
}
