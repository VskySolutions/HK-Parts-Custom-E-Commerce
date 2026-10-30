using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Stock held for a product (or specific variant) at a specific store/warehouse (REQ-CAT-011,
/// AC-CAT-011.1). The Inventory Service decrements on order confirmation, restores on pre-shipment
/// cancellation, and raises a low-stock alert when <see cref="StockQuantity"/> falls at or below
/// <see cref="LowStockThreshold"/>.
/// </summary>
public class InventoryLevel : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Null for a simple product; set when tracking a specific variant.</summary>
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    public int StockQuantity { get; set; }

    /// <summary>Configurable low-stock threshold; a value of 0 disables alerting (AC-CAT-011.2).</summary>
    public int LowStockThreshold { get; set; }

    /// <summary>Quantity reserved by confirmed-but-not-yet-shipped orders.</summary>
    public int ReservedQuantity { get; set; }

    /// <summary>Guards against repeated low-stock alerts until stock is replenished above threshold.</summary>
    public bool LowStockAlerted { get; set; }
}
