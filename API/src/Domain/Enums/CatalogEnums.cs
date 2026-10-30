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
