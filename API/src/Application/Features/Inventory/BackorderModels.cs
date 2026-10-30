namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>Backorder configuration for a product or variant (AC-CAT-013.1/013.3).</summary>
public class BackorderConfigDto
{
    public Guid Id { get; set; }
    public bool AllowBackorder { get; set; }
    public DateTime? EstimatedRestockDate { get; set; }
}

/// <summary>One open order line awaiting backordered stock (AC-ORD-006.4).</summary>
public class BackorderQueueRowDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public Guid StoreId { get; set; }
    public int Quantity { get; set; }
    public DateTime PlacedOnUtc { get; set; }
    public DateTime? EstimatedRestockDate { get; set; }
}
