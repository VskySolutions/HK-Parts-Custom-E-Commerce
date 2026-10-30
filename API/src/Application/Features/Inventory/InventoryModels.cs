using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>Stock held for a product (or specific variant) at a single store.</summary>
public class InventoryLevelDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public Guid StoreId { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; }
    public int ReservedQuantity { get; set; }
    public bool LowStockAlerted { get; set; }

    public static InventoryLevelDto From(InventoryLevel l) => new()
    {
        Id = l.Id,
        ProductId = l.ProductId,
        ProductVariantId = l.ProductVariantId,
        StoreId = l.StoreId,
        StockQuantity = l.StockQuantity,
        LowStockThreshold = l.LowStockThreshold,
        ReservedQuantity = l.ReservedQuantity,
        LowStockAlerted = l.LowStockAlerted,
    };
}
