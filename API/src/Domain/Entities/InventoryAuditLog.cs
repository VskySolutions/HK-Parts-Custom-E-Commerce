using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// One stock and/or price change made through the barcode inventory scanner (port of the PHP
/// <c>inventory_audit_log</c>). <see cref="AuditableEntity.CreatedById"/> is the admin who made it and
/// <see cref="AuditableEntity.CreatedOnUtc"/> when. Quantities are the on-hand of the store row that was
/// written (<see cref="StoreId"/>); prices are the product's (or variant's) base price.
/// </summary>
public class InventoryAuditLog : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Set when the scanned code matched a variant's SKU/GTIN rather than the product's.</summary>
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    /// <summary>The store whose stock row was written (null for a price-only change).</summary>
    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }

    public int? OldQty { get; set; }
    public int? NewQty { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }

    public InventoryAuditAction Action { get; set; }

    /// <summary>The barcode / SKU that was scanned or typed to reach the product.</summary>
    public string? ScannedCode { get; set; }
    public string? Notes { get; set; }

    /// <summary>Camera / gallery photo / typed SKU / handheld scanner.</summary>
    public InventoryScanSource Source { get; set; } = InventoryScanSource.Camera;
    /// <summary>Whether <see cref="ScannedCode"/> matched a SKU or a barcode (GTIN); null when unknown.</summary>
    public InventoryMatchType? MatchedBy { get; set; }
    /// <summary>Caller IP (from the connection, never the request body).</summary>
    public string? ClientIp { get; set; }
    /// <summary>Caller User-Agent, truncated to 300 characters.</summary>
    public string? UserAgent { get; set; }
}
