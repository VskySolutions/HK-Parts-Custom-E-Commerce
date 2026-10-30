using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using CartEntity = HKPartsEcommerce.Domain.Entities.Cart;
using CartItemEntity = HKPartsEcommerce.Domain.Entities.CartItem;

namespace HKPartsEcommerce.Application.Features.Cart;

/// <summary>
/// Shared cart-resolution and projection logic for the Cart feature (REQ-CHK-001). An authenticated
/// caller's cart is keyed by their <see cref="Customer"/> — so it is restored on login (AC-CHK-001.4);
/// a guest's cart is keyed by a client-supplied session id (a <c>sessionId</c> query/body parameter or
/// the <c>X-Cart-Session</c> header). Kept as a static helper so it needs no DI registration and each
/// MediatR handler can inject only <see cref="IApplicationDbContext"/> and <see cref="ICurrentUserService"/>.
/// </summary>
internal static class CartResolver
{
    /// <summary>Finds the caller's active cart, creating (and tracking, not yet saved) an empty one if none exists.</summary>
    public static Task<CartEntity> ResolveOrCreateAsync(
        IApplicationDbContext db, ICurrentUserService current, string? sessionId, CancellationToken ct)
        => ResolveAsync(db, current, sessionId, create: true, ct);

    /// <summary>Finds the caller's active cart, throwing <see cref="NotFoundException"/> if none exists.</summary>
    public static Task<CartEntity> ResolveExistingAsync(
        IApplicationDbContext db, ICurrentUserService current, string? sessionId, CancellationToken ct)
        => ResolveAsync(db, current, sessionId, create: false, ct);

    private static async Task<CartEntity> ResolveAsync(
        IApplicationDbContext db, ICurrentUserService current, string? sessionId, bool create, CancellationToken ct)
    {
        // Authenticated buyer: resolve the cart through the customer profile so it is persisted and
        // restored across sessions/devices (AC-CHK-001.4).
        if (current.UserId is Guid userId)
        {
            var customer = await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customer.Id && !c.IsCheckedOut, ct);

            if (cart is not null)
                return cart;
            if (!create)
                throw new NotFoundException("No active cart exists for the current user.");

            cart = new CartEntity { CustomerId = customer.Id };
            db.Carts.Add(cart);
            return cart;
        }

        // Guest: resolve by the client-supplied session id.
        var session = NormalizeGuestSession(sessionId);

        var guestCart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SessionId == session && c.CustomerId == null && !c.IsCheckedOut, ct);

        if (guestCart is not null)
            return guestCart;
        if (!create)
            throw new NotFoundException("No active cart exists for the supplied session id.");

        guestCart = new CartEntity { SessionId = session };
        db.Carts.Add(guestCart);
        return guestCart;
    }

    private static string NormalizeGuestSession(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ValidationException(new[]
            {
                new ValidationFailure("sessionId",
                    "A cart session id is required for guest carts. Provide it via the 'sessionId' query parameter or the 'X-Cart-Session' header."),
            });
        return sessionId.Trim();
    }

    /// <summary>
    /// Projects a resolved cart into a <see cref="CartDto"/>, re-checking each line against the live
    /// catalog: lines whose product/variant is missing, unpublished or out of stock are flagged
    /// <c>Available = false</c> with a matching warning (AC-CHK-001.5), and a line whose quantity exceeds
    /// the available (non-backorder) stock stays available but raises a best-effort warning (AC-CHK-001.6).
    /// <para>
    /// Customer Group pricing is overlaid at projection time (AC-CUS-003.5) so the cart shows the same price
    /// the buyer will pay at checkout, and so a group change is reflected immediately. The stored
    /// <see cref="CartItemEntity.UnitPrice"/> snapshot stays the BASE price — checkout re-resolves the group
    /// price from it, and discounting a discounted snapshot would apply the rule twice.
    /// </para>
    /// </summary>
    public static async Task<CartDto> BuildDtoAsync(
        IApplicationDbContext db, ICustomerGroupService groups, ICommerceModeService commerce,
        CartEntity cart, CancellationToken ct)
    {
        // Inquiry lines are quoted, not sold, so stock never gates them (REQ-INQ-001).
        var tenantInquiryOnly = await commerce.IsInquiryOnlyAsync(ct);

        var items = cart.Items.OrderBy(i => i.CreatedOnUtc).ToList();

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var variantIds = items.Where(i => i.ProductVariantId.HasValue)
            .Select(i => i.ProductVariantId!.Value).Distinct().ToList();

        var products = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : await db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Include(p => p.Pictures).ThenInclude(pic => pic.Media)
                .Include(p => p.InventoryLevels)
                .AsSplitQuery()
                .ToDictionaryAsync(p => p.Id, ct);

        var variants = variantIds.Count == 0
            ? new Dictionary<Guid, ProductVariant>()
            : await db.ProductVariants.AsNoTracking()
                .Where(v => variantIds.Contains(v.Id))
                .Include(v => v.InventoryLevels)
                .ToDictionaryAsync(v => v.Id, ct);

        // One batch resolve for the whole cart; empty (and free) for guests and non-group members.
        var groupId = await groups.GetCurrentGroupIdAsync(ct);
        var memberPrices = groupId is null || items.Count == 0
            ? new Dictionary<GroupPriceKey, decimal>()
            : await groups.ResolvePricesAsync(
                items.Select(i => new GroupPriceRequest(i.ProductId, i.ProductVariantId, i.UnitPrice)).ToList(),
                groupId, ct);

        var itemDtos = new List<CartItemDto>(items.Count);
        var warnings = new List<string>();

        foreach (var item in items)
        {
            products.TryGetValue(item.ProductId, out var product);
            ProductVariant? variant = null;
            if (item.ProductVariantId is Guid variantId)
                variants.TryGetValue(variantId, out variant);

            var isInquiryLine = tenantInquiryOnly || (product?.IsInquiryOnly ?? false);
            var (available, name, sku, stock, allowBackorder) =
                Evaluate(item, product, variant, warnings, isInquiryLine);
            var unitPrice = memberPrices.TryGetValue(new GroupPriceKey(item.ProductId, item.ProductVariantId), out var mp)
                ? mp
                : item.UnitPrice;

            itemDtos.Add(CartItemDto.From(
                item, name, sku, available, PrimaryImage(product, item.ProductVariantId), stock, allowBackorder,
                unitPrice, isInquiryLine));
        }

        return CartDto.From(cart, itemDtos, warnings);
    }

    /// <summary>
    /// The line's display image: a variant-specific image when the line is a variant and one exists,
    /// otherwise the product's primary (product-level) image. Only image media is considered.
    /// </summary>
    private static string? PrimaryImage(Product? product, Guid? variantId)
    {
        if (product is null)
            return null;

        var images = product.Pictures
            .Where(pic => pic.Media != null && pic.Media.MediaType == MediaType.Image);

        if (variantId.HasValue)
        {
            var variantImage = images
                .Where(pic => pic.ProductVariantId == variantId.Value)
                .OrderBy(pic => pic.DisplayOrder)
                .Select(pic => pic.Media!.Url)
                .FirstOrDefault();
            if (variantImage is not null)
                return variantImage;
        }

        return images
            .Where(pic => pic.ProductVariantId == null)
            .OrderBy(pic => pic.DisplayOrder)
            .Select(pic => pic.Media!.Url)
            .FirstOrDefault();
    }

    /// <param name="isInquiryLine">
    /// True when the tenant sells by inquiry or the product itself is quote-only. Such a line is a
    /// quote request, not a sale, so stock is neither checked nor warned about (REQ-INQ-001) — only
    /// catalog availability (deleted / unpublished / disabled variant) still applies.
    /// </param>
    private static (bool available, string name, string? sku, int stock, bool allowBackorder) Evaluate(
        CartItemEntity item, Product? product, ProductVariant? variant, List<string> warnings,
        bool isInquiryLine)
    {
        // Soft-deleted products/variants are excluded by the global query filter, so a null lookup means
        // the catalog entry is gone entirely.
        if (product is null)
        {
            warnings.Add("A product in your cart is no longer available and has been marked unavailable.");
            return (false, "Unavailable product", null, 0, false);
        }

        var name = product.Name;
        var sku = variant?.Sku ?? product.Sku;
        var stock = variant?.StockQuantity ?? product.StockQuantity;
        var allowBackorder = variant?.AllowBackorder ?? product.AllowBackorder;

        if (!product.IsPublished)
        {
            warnings.Add($"'{name}' is no longer available and has been marked unavailable.");
            return (false, name, sku, stock, allowBackorder);
        }

        if (item.ProductVariantId.HasValue && (variant is null || !variant.IsEnabled))
        {
            warnings.Add($"A selected option for '{name}' is no longer available.");
            return (false, name, sku, stock, allowBackorder);
        }

        // A quote request is not a sale: skip every stock rule below it.
        if (isInquiryLine)
            return (true, name, sku, stock, allowBackorder);

        if (stock <= 0 && !allowBackorder)
        {
            warnings.Add($"'{name}' is out of stock.");
            return (false, name, sku, stock, allowBackorder);
        }

        // Best-effort quantity check: no explicit per-product max exists, so flag a request that
        // exceeds current stock without blocking the line (AC-CHK-001.6).
        if (!allowBackorder && item.Quantity > stock)
            warnings.Add($"Only {stock} unit(s) of '{name}' are in stock; your cart requests {item.Quantity}.");

        return (true, name, sku, stock, allowBackorder);
    }
}
