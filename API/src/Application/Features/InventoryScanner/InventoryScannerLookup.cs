using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.InventoryScanner;

/// <summary>Shared scanner logic: resolve a scanned code, pick the store row, build the product snapshot.</summary>
internal static class InventoryScannerLookup
{
    /// <summary>Most candidates returned for an ambiguous code (409).</summary>
    internal const int MaxCandidates = 10;

    /// <summary>Longest code accepted by lookup (and stored as the audit row's ScannedCode).</summary>
    internal const int MaxCodeLength = 100;

    // Custom-field codes (from the PHP migration) that hold the product's model number, in preference order.
    private static readonly string[] ModelFieldCodes = { "model", "manufacturer_model_no" };

    /// <summary>A code resolved to a product (and, for a variant SKU/GTIN, the variant).</summary>
    internal sealed record Hit(Guid ProductId, Guid? VariantId, InventoryMatchType MatchedBy);

    /// <summary>
    /// Every product / variant the code identifies, SKU matches before barcode (GTIN) matches, at most
    /// <see cref="MaxCandidates"/>. Exact match on the trimmed code; case-insensitive through the column
    /// collation. Soft-deleted products/variants are filtered out. When nothing matches a 12-digit UPC-A
    /// is retried in its 13-digit EAN form (leading 0) and vice versa, since labels and catalog data often
    /// disagree on which form they store.
    /// </summary>
    internal static async Task<List<Hit>> FindAllAsync(IApplicationDbContext db, string? code, CancellationToken ct)
    {
        var c = code?.Trim();
        if (string.IsNullOrEmpty(c)) return new List<Hit>();

        var hits = await MatchAsync(db, c, ct);
        if (hits.Count == 0 && c.All(char.IsAsciiDigit))
        {
            if (c.Length == 12) hits = await MatchAsync(db, "0" + c, ct);
            else if (c.Length == 13 && c[0] == '0') hits = await MatchAsync(db, c[1..], ct);
        }
        return hits;
    }

    private static async Task<List<Hit>> MatchAsync(IApplicationDbContext db, string c, CancellationToken ct)
    {
        // Active products first so a candidate picker leads with the likely one.
        var productsBySku = await db.Products.AsNoTracking().Where(p => p.Sku == c)
            .OrderByDescending(p => p.IsPublished).ThenBy(p => p.Name)
            .Select(p => p.Id).Take(MaxCandidates).ToListAsync(ct);
        var variantsBySku = await db.ProductVariants.AsNoTracking()
            .Where(v => v.Sku == c && v.Product != null && !v.Product.Deleted)
            .OrderBy(v => v.ProductId).ThenBy(v => v.DisplayOrder)
            .Select(v => new { v.Id, v.ProductId }).Take(MaxCandidates).ToListAsync(ct);
        var productsByGtin = await db.Products.AsNoTracking().Where(p => p.Gtin == c)
            .OrderByDescending(p => p.IsPublished).ThenBy(p => p.Name)
            .Select(p => p.Id).Take(MaxCandidates).ToListAsync(ct);
        var variantsByGtin = await db.ProductVariants.AsNoTracking()
            .Where(v => v.Gtin == c && v.Product != null && !v.Product.Deleted)
            .OrderBy(v => v.ProductId).ThenBy(v => v.DisplayOrder)
            .Select(v => new { v.Id, v.ProductId }).Take(MaxCandidates).ToListAsync(ct);

        var all = productsBySku.Select(id => new Hit(id, null, InventoryMatchType.Sku))
            .Concat(variantsBySku.Select(v => new Hit(v.ProductId, v.Id, InventoryMatchType.Sku)))
            .Concat(productsByGtin.Select(id => new Hit(id, null, InventoryMatchType.Barcode)))
            .Concat(variantsByGtin.Select(v => new Hit(v.ProductId, v.Id, InventoryMatchType.Barcode)));

        // A row whose SKU and GTIN are the same code is one candidate (matched by SKU), not two.
        return all.DistinctBy(h => (h.ProductId, h.VariantId)).Take(MaxCandidates).ToList();
    }

    /// <summary>
    /// Resolves a code without throwing: <see cref="ScannerLookupResult.Product"/> for exactly one match,
    /// <see cref="ScannerLookupResult.Candidates"/> when several products share the code (never silently
    /// picks one), neither when nothing matches. An unknown code is an everyday scanner outcome, not an
    /// error, so the controller maps it to 404 / 409 itself.
    /// </summary>
    internal static async Task<ScannerLookupResult> ResolveAsync(IApplicationDbContext db, string code, CancellationToken ct)
    {
        var trimmed = code.Trim();
        var hits = await FindAllAsync(db, trimmed, ct);

        var products = new List<ScannedProductDto>(hits.Count);
        foreach (var hit in hits)
        {
            var dto = await BuildAsync(db, hit.ProductId, hit.VariantId, ct);
            if (dto is null) continue;
            dto.MatchedBy = hit.MatchedBy;
            products.Add(dto);
        }

        return products.Count switch
        {
            0 => new ScannerLookupResult { Code = trimmed, Message = $"Product not found for barcode \"{trimmed}\"." },
            1 => new ScannerLookupResult { Code = trimmed, Product = products[0] },
            _ => new ScannerLookupResult
            {
                Code = trimmed,
                Candidates = products,
                Message = $"{products.Count} products match \"{trimmed}\". Choose the one you scanned.",
            },
        };
    }

    /// <summary>
    /// The store the scanner writes stock to: the first store by name — the same "primary store" the
    /// admin product page's single "Qty in stock" field edits.
    /// </summary>
    internal static Task<Store?> PrimaryStoreAsync(IApplicationDbContext db, CancellationToken ct) =>
        db.Stores.AsNoTracking().OrderBy(s => s.Name).FirstOrDefaultAsync(ct);

    /// <summary>Builds the snapshot for a product/variant; null when it no longer exists.</summary>
    internal static async Task<ScannedProductDto?> BuildAsync(IApplicationDbContext db, Guid productId, Guid? variantId, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Pictures).ThenInclude(pic => pic.Media)
            .Include(p => p.CustomFieldValues).ThenInclude(v => v.ProductCustomField)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) return null;

        ProductVariant? variant = null;
        if (variantId is Guid vid)
        {
            variant = await db.ProductVariants.AsNoTracking()
                .Include(v => v.AttributeValues).ThenInclude(av => av.ProductAttributeValue).ThenInclude(pav => pav!.ProductAttribute)
                .FirstOrDefaultAsync(v => v.Id == vid && v.ProductId == productId, ct);
            if (variant is null) return null;
        }

        var store = await PrimaryStoreAsync(db, ct);
        var levels = await db.InventoryLevels.AsNoTracking()
            .Where(l => l.ProductId == productId && l.ProductVariantId == variantId)
            .Select(l => new { l.StoreId, l.StockQuantity })
            .ToListAsync(ct);
        var primaryQty = store is null ? 0 : levels.Where(l => l.StoreId == store.Id).Sum(l => l.StockQuantity);
        var split = levels.Any(l => (store is null || l.StoreId != store.Id) && l.StockQuantity > 0);

        // Qty can't be edited when: no store exists; stock lives in several stores; or a variant
        // product was scanned by its own code (stock is per variant — scan the variant's barcode).
        string? locked = null;
        if (store is null)
            locked = "No store exists yet — create a store before setting stock.";
        else if (variant is null && product.ProductType == ProductType.WithVariants)
            locked = "This product's stock is kept per variant — scan the variant's barcode / SKU instead.";
        else if (split)
            locked = "Stock for this item is held in several stores — update it from the product's Inventory tab.";

        var images = product.Pictures
            .Where(p => p.Media != null && p.Media.MediaType == MediaType.Image)
            .OrderBy(p => variant != null && p.ProductVariantId == variant.Id ? 0 : 1)
            .ThenBy(p => p.ProductVariantId == null ? 0 : 1)
            .ThenBy(p => p.DisplayOrder);

        var model = ModelFieldCodes
            .Select(code => product.CustomFieldValues.FirstOrDefault(v =>
                v.ProductCustomField != null && v.ProductCustomField.Code == code && !string.IsNullOrWhiteSpace(v.Value))?.Value)
            .FirstOrDefault(v => v != null);

        return new ScannedProductDto
        {
            ProductId = product.Id,
            ProductVariantId = variant?.Id,
            VariantLabel = variant is null ? null : VariantLabel(variant),
            ProductType = product.ProductType,
            Name = product.Name,
            Model = model,
            Sku = variant?.Sku ?? product.Sku,
            Barcode = variant?.Gtin ?? product.Gtin,
            Msrp = variant?.Msrp ?? product.Msrp,
            Price = variant is null ? product.Price : (variant.Price ?? product.Price),
            IsPublished = product.IsPublished,
            ImageUrl = images.Select(p => p.Media!.Url).FirstOrDefault(),
            QtyInStock = split ? levels.Sum(l => l.StockQuantity) : primaryQty,
            StoreId = store?.Id,
            StoreName = store?.Name,
            StockSplit = split,
            CanEditQty = locked is null,
            QtyLockedReason = locked,
        };
    }

    internal static string VariantLabel(ProductVariant v) => string.Join(" / ", v.AttributeValues
        .Where(av => av.ProductAttributeValue != null)
        .OrderBy(av => av.ProductAttributeValue!.ProductAttribute?.Name)
        .Select(av => av.ProductAttributeValue!.ProductAttribute is { } a
            ? $"{a.Name}: {av.ProductAttributeValue.Value}"
            : av.ProductAttributeValue.Value));

    /// <summary>One audit row with the product's current state (the success screen); null when missing.</summary>
    internal static async Task<InventoryScanLogDto?> GetLogAsync(IApplicationDbContext db, Guid id, CancellationToken ct)
    {
        var log = (await ProjectLogsAsync(db, db.InventoryAuditLogs.Where(l => l.Id == id), ct)).FirstOrDefault();
        if (log is not null)
            log.Product = await BuildAsync(db, log.ProductId, log.ProductVariantId, ct);
        return log;
    }

    /// <summary>Projects audit rows (with product/variant/store/admin) into list DTOs, newest first.</summary>
    internal static async Task<List<InventoryScanLogDto>> ProjectLogsAsync(IApplicationDbContext db, IQueryable<InventoryAuditLog> logs, CancellationToken ct)
    {
        // IgnoreQueryFilters: a log must still show after its product/variant/store is soft-deleted
        // (otherwise the required Product join would drop the row from history entirely).
        var rows = await logs.IgnoreQueryFilters()
            .Include(l => l.Product)
            .Include(l => l.Store)
            .Include(l => l.ProductVariant).ThenInclude(v => v!.AttributeValues)
                .ThenInclude(av => av.ProductAttributeValue).ThenInclude(pav => pav!.ProductAttribute)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(ct);

        var adminIds = rows.Where(r => r.CreatedById != null).Select(r => r.CreatedById!.Value).Distinct().ToList();
        var admins = adminIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => adminIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        return rows
            .OrderByDescending(r => r.CreatedOnUtc).ThenByDescending(r => r.Id)
            .Select(r => new InventoryScanLogDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductVariantId = r.ProductVariantId,
                ProductName = r.Product?.Name ?? "(deleted product)",
                VariantLabel = r.ProductVariant is null ? null : VariantLabel(r.ProductVariant),
                Sku = r.ProductVariant?.Sku ?? r.Product?.Sku,
                Barcode = r.ProductVariant?.Gtin ?? r.Product?.Gtin,
                StoreName = r.Store?.Name,
                OldQty = r.OldQty,
                NewQty = r.NewQty,
                QtyChange = (r.NewQty ?? 0) - (r.OldQty ?? 0),
                OldPrice = r.OldPrice,
                NewPrice = r.NewPrice,
                Action = r.Action,
                Source = r.Source,
                MatchedBy = r.MatchedBy,
                ScannedCode = r.ScannedCode,
                Notes = r.Notes,
                AdminName = r.CreatedById is Guid id && admins.TryGetValue(id, out var name) ? name : null,
                CreatedOnUtc = r.CreatedOnUtc,
            }).ToList();
    }
}
