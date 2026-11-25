using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>One cart line to check: what, how many, and the name to show when it is short.</summary>
public record StockCheckLine(Guid ProductId, Guid? VariantId, int Quantity, string Name);

/// <summary>
/// PHP parity (legacy <c>EcommCart::checkInventory</c>): the old site kept ONE stock number per product
/// (<c>qty_in_stock</c>). Here stock is per store, so the same rule runs on the product's TOTAL over every enabled
/// store, less what is reserved. An item is short when that total is 0 or less, it is marked out of stock, or the
/// quantity is more than the total, unless it allows backorders (or is marked backordered). The first short item is
/// returned so the message can name it, as the old site did.
/// </summary>
public static class TotalStockCheck
{
    public const string MessagePrefix = "We are sorry, but some item(s) in your cart are now out of stock: ";

    public static async Task<string?> FirstShortItemAsync(
        IApplicationDbContext db, IReadOnlyList<StockCheckLine> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
            return null;

        // Quantities of the same product / variant on several lines add up.
        var wanted = lines
            .GroupBy(l => (l.ProductId, l.VariantId))
            .Select(g => (g.Key.ProductId, g.Key.VariantId, Qty: g.Sum(l => l.Quantity), Name: g.First().Name))
            .ToList();
        var productIds = wanted.Select(w => w.ProductId).Distinct().ToList();

        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.AllowBackorder, p.StockStatusOverride })
            .ToDictionaryAsync(p => p.Id, ct);
        var variantIds = wanted.Where(w => w.VariantId != null).Select(w => w.VariantId!.Value).Distinct().ToList();
        var variants = await db.ProductVariants.AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.AllowBackorder })
            .ToDictionaryAsync(v => v.Id, ct);

        var totals = (await db.InventoryLevels.AsNoTracking()
                .Where(l => productIds.Contains(l.ProductId) && l.Store != null && l.Store.IsEnabled)
                .GroupBy(l => new { l.ProductId, l.ProductVariantId })
                .Select(g => new { g.Key.ProductId, g.Key.ProductVariantId, Available = g.Sum(l => l.StockQuantity - l.ReservedQuantity) })
                .ToListAsync(ct))
            .ToDictionary(x => (x.ProductId, x.ProductVariantId), x => x.Available);

        foreach (var w in wanted)
        {
            if (!products.TryGetValue(w.ProductId, out var product))
                continue; // a removed product is refused elsewhere

            var backorder = w.VariantId is Guid vid && variants.TryGetValue(vid, out var variant)
                ? variant.AllowBackorder
                : product.AllowBackorder;
            if (backorder || product.StockStatusOverride == StockStatus.Backordered)
                continue;

            var available = totals.TryGetValue((w.ProductId, w.VariantId), out var total) ? total : 0;
            if (available <= 0 || product.StockStatusOverride == StockStatus.OutOfStock || w.Qty > available)
                return w.Name;
        }
        return null;
    }
}
