using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A line within an <see cref="Order"/>. Product name/SKU/price are snapshotted at placement time so
/// the order is immutable to later catalog edits. <see cref="ProductId"/>/<see cref="ProductVariantId"/>
/// are stored as plain references (no FK) to preserve the order if a product is later removed.
/// </summary>
public class OrderLineItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }

    /// <summary>The unit price actually charged — the Customer Group member price when the buyer is in a
    /// discounting group, otherwise the list price. <see cref="LineTotal"/> is this × <see cref="Quantity"/>.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>The list (pre-discount) unit price, snapshotted at placement. Equals <see cref="UnitPrice"/>
    /// when no group discount applied. Kept so the order records what the item would have cost at list.</summary>
    public decimal OriginalUnitPrice { get; set; }

    /// <summary>The Customer Group saving on this line = (<see cref="OriginalUnitPrice"/> − <see cref="UnitPrice"/>)
    /// × <see cref="Quantity"/>; 0 when no group discount applied. Persisted so the saving is itemizable on the
    /// order/invoice without re-deriving it from live pricing (WO-22).</summary>
    public decimal DiscountAmount { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>Units of this line already dispatched across shipments; drives partial-fulfilment state (WO-46).</summary>
    public int QuantityShipped { get; set; }

    /// <summary>
    /// The values the buyer typed into the product's CustomInput attributes, carried over from the cart
    /// line as a JSON array of { attributeId, name, value } (null when there were none). Like the rest of
    /// the line it is a snapshot — the attribute name is stored, so renaming or deleting the attribute
    /// never changes what the order says was ordered.
    /// </summary>
    public string? CustomAttributesJson { get; set; }
}
