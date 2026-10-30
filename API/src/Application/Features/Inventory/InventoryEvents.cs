using MediatR;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>
/// Raised when a (product/variant, store) inventory level rises from ≤0 (backordered/out of stock) back
/// to positive stock (AC-CAT-013.4). The backorder fulfilment worker (WO-86) subscribes to this to release
/// waiting orders in FIFO order; publishing with no subscriber is a harmless no-op.
/// </summary>
public record InventoryReplenished(
    Guid ProductId,
    Guid? ProductVariantId,
    Guid StoreId,
    int QuantityAdded) : INotification;
