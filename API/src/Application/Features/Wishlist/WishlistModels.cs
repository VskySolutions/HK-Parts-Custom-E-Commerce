using WishlistEntity = HKPartsEcommerce.Domain.Entities.Wishlist;
using WishlistItemEntity = HKPartsEcommerce.Domain.Entities.WishlistItem;

namespace HKPartsEcommerce.Application.Features.Wishlist;

/// <summary>The current customer's wishlist and its saved items (REQ-CHK-002).</summary>
public class WishlistDto
{
    public Guid Id { get; set; }
    public List<WishlistItemDto> Items { get; set; } = new();

    public static WishlistDto From(WishlistEntity wishlist, IEnumerable<WishlistItemDto> items) => new()
    {
        Id = wishlist.Id,
        Items = items.ToList(),
    };
}

/// <summary>A saved product/variant in the wishlist, resolved against the live catalog.</summary>
public class WishlistItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal Price { get; set; }

    /// <summary>False when the product/variant is gone, unpublished or a disabled variant.</summary>
    public bool Available { get; set; }

    public static WishlistItemDto From(WishlistItemEntity item, string name, string? sku, decimal price, bool available) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductVariantId = item.ProductVariantId,
        Name = name,
        Sku = sku,
        Price = price,
        Available = available,
    };
}
