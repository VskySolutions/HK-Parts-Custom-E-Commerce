using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Ebay;

/// <summary>Turns stored media URLs into the public HTTPS URLs eBay fetches images from.</summary>
public interface IEbayPublicUrlProvider
{
    /// <summary>Absolute URLs pass through; relative ones are prefixed with the configured image origin. Null when unusable.</summary>
    string? ToPublicImageUrl(string? url);

    /// <summary>Where the OAuth callback sends the admin back to (the eBay settings page).</summary>
    string AdminSettingsUrl(string? query = null);
}

/// <summary>Signs and verifies the OAuth <c>state</c> parameter (CSRF): store, user, single-use nonce, 15-minute life.</summary>
public interface IEbayOAuthStateProtector
{
    string Create(Guid storeId, Guid? userId);

    /// <summary>The store the flow was started for; throws <see cref="ForbiddenAccessException"/> when invalid, expired or replayed.</summary>
    Guid Validate(string state);
}

public static class EbaySupport
{
    public const string AlreadyListedMessage = "This product is already listed on eBay";
    public const string NotEligibleMessage =
        "You can not list this product, as the inventory level is 0 or is listed as out of stock";
    public const int MaxImages = 12;
    public const int MaxTitleLength = 80;

    /// <summary>
    /// The store an eBay request acts on: the requested one when it exists, otherwise the primary store (the
    /// first-created enabled store). 404 when no store exists at all.
    /// </summary>
    public static async Task<Guid> ResolveStoreAsync(IApplicationDbContext db, Guid? requested, CancellationToken ct)
    {
        if (requested is Guid id)
        {
            if (await db.Stores.AsNoTracking().AnyAsync(s => s.Id == id, ct))
                return id;
            throw new NotFoundException(nameof(Store), id);
        }

        var primary = await db.Stores.AsNoTracking()
            .OrderByDescending(s => s.IsEnabled).ThenBy(s => s.CreatedOnUtc)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
        return primary ?? throw new NotFoundException("No store is configured. Create a store first.");
    }

    /// <summary>On-hand stock of the product itself (not its variants) in one store.</summary>
    public static IQueryable<int> StoreQuantity(IApplicationDbContext db, Guid storeId, Guid productId) =>
        db.InventoryLevels.Where(l => l.StoreId == storeId && l.ProductId == productId && l.ProductVariantId == null)
            .Select(l => l.StockQuantity);

    /// <summary>
    /// Products that may be listed: in the store (no store rows = every store), Simple or Bundle (Grouped),
    /// published, not forced out of stock, store stock &gt; 0, and with no non-ended listing.
    /// </summary>
    public static IQueryable<Product> EligibleProducts(IApplicationDbContext db, Guid storeId) =>
        db.Products.Where(p =>
            (p.ProductType == ProductType.Simple || p.ProductType == ProductType.Grouped)
            && p.IsPublished
            && p.StockStatusOverride != StockStatus.OutOfStock
            && (!p.StoreMappings.Any() || p.StoreMappings.Any(m => m.StoreId == storeId))
            && db.InventoryLevels
                .Where(l => l.StoreId == storeId && l.ProductId == p.Id && l.ProductVariantId == null)
                .Sum(l => (int?)l.StockQuantity) > 0
            && !db.EbayListings.Any(l => l.ProductId == p.Id && l.Status != EbayListingStatus.Ended));

    public static string? ItemUrl(EbayEnvironment environment, string? listingId) =>
        string.IsNullOrEmpty(listingId)
            ? null
            : (environment == EbayEnvironment.Production ? "https://www.ebay.com/itm/" : "https://sandbox.ebay.com/itm/") + listingId;

    public static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max].TrimEnd();

    /// <summary>Recomputes <see cref="Product.ChannelEbay"/> from the product's remaining non-ended listings.</summary>
    public static async Task SyncChannelFlagAsync(IApplicationDbContext db, Guid productId, Guid? exceptListingId, CancellationToken ct)
    {
        var listed = await db.EbayListings.AnyAsync(
            l => l.ProductId == productId && l.Status != EbayListingStatus.Ended && l.Id != exceptListingId, ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is not null)
            product.ChannelEbay = listed;
    }

    public static Domain.Entities.AuditTrail Audit(
        string action, Guid listingId, object? before, object? after, ICurrentUserService user, IDateTimeProvider clock) => new()
    {
        UserId = user.UserId,
        ActorName = user.Email,
        Action = action,
        EntityType = nameof(EbayListing),
        EntityId = listingId.ToString(),
        MetadataJson = JsonSerializer.Serialize(new { before, after }),
        TimestampUtc = clock.UtcNow,
    };

    /// <summary>The 422 thrown for eBay business errors (error severity only).</summary>
    public static ExternalBusinessException BusinessError(EbayApiException ex) =>
        new("ERROR - Your listing has returned errors:", ex.Errors);

    public static string JoinErrors(IEnumerable<ExternalError> errors) =>
        string.Join(Environment.NewLine, errors.Select(e => string.IsNullOrEmpty(e.Code) ? e.Message : $"[{e.Code}] {e.Message}"));
}
