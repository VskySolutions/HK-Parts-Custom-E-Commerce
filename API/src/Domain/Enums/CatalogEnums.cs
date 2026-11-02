namespace HKPartsEcommerce.Domain.Enums;

/// <summary>The five product types supported by the catalog (Catalog Management REQ-CAT-001).</summary>
public enum ProductType
{
    Simple = 0,
    Grouped = 1,
    WithVariants = 2,
    Downloadable = 3,
    GiftCard = 4
}

/// <summary>Gift card valuation mode (AC-CAT-001.5).</summary>
public enum GiftCardType
{
    Fixed = 0,
    OpenAmount = 1
}

/// <summary>Product-to-product relationship classification (REQ-CAT-007).</summary>
public enum ProductRelationshipType
{
    Related = 0,
    CrossSell = 1,
    UpSell = 2
}

/// <summary>Media kind for a product/variant gallery entry (REQ-CAT-012).</summary>
public enum ProductMediaType
{
    Image = 0,
    Video = 1
}

/// <summary>Availability of a product/variant derived from inventory (Catalog Management contracts).</summary>
public enum StockStatus
{
    InStock = 0,
    OutOfStock = 1,
    Backordered = 2
}

/// <summary>Input type of a <see cref="Entities.ProductCustomField"/>.</summary>
public enum ProductCustomFieldType
{
    Text = 0,
    TextArea = 1
}

/// <summary>What a barcode-scanner inventory update changed (PHP inventory_audit_log.action).</summary>
public enum InventoryAuditAction
{
    QtyUpdate = 1,
    PriceUpdate = 2,
    Both = 3
}

/// <summary>How the barcode scanner reached the product (recorded on each inventory audit row).</summary>
public enum InventoryScanSource
{
    /// <summary>Live camera decode.</summary>
    Camera = 1,
    /// <summary>Decoded from an uploaded / gallery photo.</summary>
    Gallery = 2,
    /// <summary>SKU typed by hand.</summary>
    Manual = 3,
    /// <summary>USB / Bluetooth keyboard-wedge handheld scanner.</summary>
    Hardware = 4
}

/// <summary>Which field the scanned code matched.</summary>
public enum InventoryMatchType
{
    Sku = 1,
    /// <summary>GTIN / UPC / EAN.</summary>
    Barcode = 2
}
