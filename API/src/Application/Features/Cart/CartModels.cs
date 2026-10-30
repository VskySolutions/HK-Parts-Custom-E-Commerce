using HKPartsEcommerce.Application.Common.Models;
using CartEntity = HKPartsEcommerce.Domain.Entities.Cart;
using CartItemEntity = HKPartsEcommerce.Domain.Entities.CartItem;

namespace HKPartsEcommerce.Application.Features.Cart;

/// <summary>
/// A shopping cart projection returned by every Cart endpoint (REQ-CHK-001). Carries the resolved
/// lines, the computed <see cref="Subtotal"/> and any availability <see cref="Warnings"/> raised while
/// re-checking each line against the live catalog (AC-CHK-001.5/6).
/// </summary>
public class CartDto
{
    public Guid Id { get; set; }
    public Guid? CustomerId { get; set; }
    public string? SessionId { get; set; }
    public string? AppliedCouponCode { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public List<CartItemDto> Items { get; set; } = new();

    /// <summary>Sum of every line's <see cref="CartItemDto.LineTotal"/>.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Human-readable notices for unavailable, unpublished or out-of-stock lines (AC-CHK-001.5).</summary>
    public List<string> Warnings { get; set; } = new();

    public static CartDto From(CartEntity cart, List<CartItemDto> items, List<string> warnings) => new()
    {
        Id = cart.Id,
        CustomerId = cart.CustomerId,
        SessionId = cart.SessionId,
        AppliedCouponCode = cart.AppliedCouponCode,
        CurrencyCode = cart.CurrencyCode,
        Items = items,
        Subtotal = items.Sum(i => i.LineTotal),
        Warnings = warnings,
    };
}

/// <summary>A single line in a <see cref="CartDto"/> with its snapshotted unit price (AC-CHK-001.1).</summary>
public class CartItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>Primary image URL for the line (variant-specific image preferred, else the product's); null when the product has no image.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Available stock for the line (variant or product) — the cart uses it to cap quantity increases.</summary>
    public int StockQuantity { get; set; }

    /// <summary>Whether the line permits ordering beyond available stock (no hard cap on quantity).</summary>
    public bool AllowBackorder { get; set; }

    /// <summary>False when the line's product/variant is missing, unpublished or out of stock (AC-CHK-001.5).</summary>
    public bool Available { get; set; }

    /// <summary>
    /// True when the line's product is quote-only (REQ-INQ-001): the cart shows an inquiry badge and
    /// checks out as a request rather than a payment. A cart may not mix these with buyable items.
    /// </summary>
    public bool IsInquiryOnly { get; set; }

    /// <summary>The values the buyer typed into the product's CustomInput attributes; empty when it has none.</summary>
    public List<CustomAttributeSelection> CustomAttributes { get; set; } = new();

    /// <param name="unitPrice">
    /// The effective unit price. Defaults to the cart's base snapshot; the caller passes the Customer Group
    /// price when the buyer belongs to a group (AC-CUS-003.5).
    /// </param>
    public static CartItemDto From(
        CartItemEntity item, string productName, string? sku, bool available,
        string? imageUrl = null, int stockQuantity = 0, bool allowBackorder = false,
        decimal? unitPrice = null, bool isInquiryOnly = false) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductVariantId = item.ProductVariantId,
        ProductName = productName,
        Sku = sku,
        Quantity = item.Quantity,
        UnitPrice = unitPrice ?? item.UnitPrice,
        LineTotal = (unitPrice ?? item.UnitPrice) * item.Quantity,
        ImageUrl = imageUrl,
        StockQuantity = stockQuantity,
        AllowBackorder = allowBackorder,
        Available = available,
        IsInquiryOnly = isInquiryOnly,
        CustomAttributes = Common.Models.CustomAttributes.Parse(item.CustomAttributesJson),
    };
}
