using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.InventoryScanner;

/// <summary>
/// A product (or variant) found by a scanned barcode / typed SKU — what the scanner's product screen
/// shows and edits. Port of the PHP InventoryController::formatProduct payload.
/// </summary>
public class ScannedProductDto
{
    public Guid ProductId { get; set; }
    /// <summary>Set when the code matched a variant's SKU/GTIN; stock and price then edit that variant.</summary>
    public Guid? ProductVariantId { get; set; }
    /// <summary>e.g. "Color: Red / Size: L" for a variant hit.</summary>
    public string? VariantLabel { get; set; }
    public ProductType ProductType { get; set; }

    public string Name { get; set; } = string.Empty;
    /// <summary>The "Model" / "Manufacturer Model Number" custom field (migrated from PHP), when set.</summary>
    public string? Model { get; set; }
    public string? Sku { get; set; }
    /// <summary>GTIN / UPC / EAN.</summary>
    public string? Barcode { get; set; }
    public decimal? Msrp { get; set; }
    public decimal? Price { get; set; }
    public bool IsPublished { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>On-hand in <see cref="StoreName"/> (the primary store), or the all-store total when <see cref="StockSplit"/>.</summary>
    public int QtyInStock { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    /// <summary>Stock is also held in another store, so a single quantity can't be written safely.</summary>
    public bool StockSplit { get; set; }

    /// <summary>False when the quantity can't be set from the scanner; <see cref="QtyLockedReason"/> says why.</summary>
    public bool CanEditQty { get; set; }
    public string? QtyLockedReason { get; set; }

    /// <summary>Lookup only: whether the scanned code matched the SKU or the barcode.</summary>
    public InventoryMatchType? MatchedBy { get; set; }

    /// <summary>
    /// Whether the caller may change the selling price (needs the Catalog module on top of Inventory).
    /// Filled in by the controller from the caller's grants; the UI makes the price read-only when false.
    /// </summary>
    public bool CanChangePrice { get; set; }
}

/// <summary>
/// Outcome of a scanner lookup: exactly one <see cref="Product"/>, several <see cref="Candidates"/>
/// (ambiguous code), or neither (not found). Returned instead of throwing for the no-match case.
/// </summary>
public class ScannerLookupResult
{
    public string Code { get; set; } = string.Empty;
    public ScannedProductDto? Product { get; set; }
    public List<ScannedProductDto> Candidates { get; set; } = new();
    /// <summary>Human-readable reason when there is no single product.</summary>
    public string? Message { get; set; }

    public bool Found => Product is not null;
    public bool IsAmbiguous => Candidates.Count > 1;
}

/// <summary>One scanner update (audit row) with the product as it is now.</summary>
public class InventoryScanLogDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantLabel { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string? StoreName { get; set; }
    public int? OldQty { get; set; }
    public int? NewQty { get; set; }
    /// <summary>NewQty − OldQty (0 for a price-only change).</summary>
    public int QtyChange { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public InventoryAuditAction Action { get; set; }
    public InventoryScanSource Source { get; set; }
    public InventoryMatchType? MatchedBy { get; set; }
    public string? ScannedCode { get; set; }
    public string? Notes { get; set; }
    public string? AdminName { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>Only on the single-log read (success screen): the product's current state.</summary>
    public ScannedProductDto? Product { get; set; }
}

/// <summary>Scanner dashboard: today's activity + the latest updates.</summary>
public class InventoryScannerDashboardDto
{
    public int ProductsUpdatedToday { get; set; }
    public int UpdatesToday { get; set; }
    public DateTime? LastUpdateUtc { get; set; }
    /// <summary>The IANA zone "today" was computed in (tenant branding's display timezone).</summary>
    public string BusinessTimeZone { get; set; } = "UTC";
    public List<InventoryScanLogDto> RecentUpdates { get; set; } = new();
}
