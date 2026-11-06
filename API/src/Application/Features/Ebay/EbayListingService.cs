using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Ebay;

public sealed record EbayInventorySyncResult(int Checked, int Updated, int Failed, IReadOnlyList<string> Errors);

/// <summary>
/// The eBay side of a listing's life: publish / revise (inventory item → compatibility → offer → publish),
/// end (withdraw), the quantity/price safety-net sync and the out-of-stock sweep. Callers own the
/// SaveChanges — every method only mutates tracked entities.
/// </summary>
public interface IEbayListingService
{
    /// <summary>Publishes an unpublished listing or revises a published one. Throws <see cref="EbayApiException"/>.</summary>
    Task PushAsync(EbayListing listing, CancellationToken ct = default);

    /// <summary>Withdraws a published offer (no eBay call for an unpublished one) and marks the listing ended.</summary>
    Task EndAsync(EbayListing listing, CancellationToken ct = default);

    /// <summary>Pushes changed quantity (and price when SyncPriceFromStore) for the store's active listings, 25 per call.</summary>
    Task<EbayInventorySyncResult> SyncInventoryAsync(Guid storeId, IReadOnlyCollection<Guid>? productIds = null, CancellationToken ct = default);

    /// <summary>Ends every active listing of the products (and of bundles containing them) whose store stock is 0. Saves.</summary>
    Task<int> EndOutOfStockAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
}

public sealed class EbayListingService : IEbayListingService
{
    private const int BulkLimit = 25;

    private readonly IApplicationDbContext _db;
    private readonly IEbayClient _ebay;
    private readonly IEbayMetadataService _metadata;
    private readonly IEbayPublicUrlProvider _urls;
    private readonly IPriceResolver _prices;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<EbayListingService> _logger;

    public EbayListingService(
        IApplicationDbContext db, IEbayClient ebay, IEbayMetadataService metadata, IEbayPublicUrlProvider urls,
        IPriceResolver prices, IDateTimeProvider clock, ILogger<EbayListingService> logger)
    {
        _db = db;
        _ebay = ebay;
        _metadata = metadata;
        _urls = urls;
        _prices = prices;
        _clock = clock;
        _logger = logger;
    }

    public static string CurrencyFor(string marketplaceId) => marketplaceId switch
    {
        "EBAY_GB" => "GBP",
        "EBAY_DE" or "EBAY_FR" or "EBAY_IT" or "EBAY_ES" or "EBAY_IE" or "EBAY_AT" or "EBAY_NL" or "EBAY_BE" => "EUR",
        "EBAY_CA" => "CAD",
        "EBAY_AU" => "AUD",
        _ => "USD",
    };

    public async Task PushAsync(EbayListing listing, CancellationToken ct = default)
    {
        var account = await _db.EbayAccounts.AsNoTracking().FirstAsync(a => a.StoreId == listing.StoreId, ct);

        var images = await _db.ProductPictures.AsNoTracking()
            .Where(p => p.ProductId == listing.ProductId && p.ProductVariantId == null && p.Media != null && p.Media.MediaType == MediaType.Image)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => p.Media!.Url)
            .Take(EbaySupport.MaxImages)
            .ToListAsync(ct);
        var imageUrls = images.Select(_urls.ToPublicImageUrl).Where(u => u is not null).Select(u => u!).ToList();

        var aspects = listing.Aspects
            .GroupBy(a => a.Name)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(a => a.Value).ToList());

        var item = new EbayInventoryItem(
            listing.Title, listing.Subtitle, listing.Description ?? string.Empty, listing.ConditionId,
            listing.Quantity, imageUrls, aspects);

        await _ebay.CreateOrReplaceInventoryItemAsync(listing.StoreId, listing.Sku, item, ct);

        if (account.FitmentEnabled)
        {
            var compat = await _metadata.GetCompatibilityAsync(listing.StoreId, listing.CategoryId, ct);
            if (compat.Supported)
            {
                var fitment = _db.VehicleFitments.AsNoTracking()
                    .Where(f => f.ProductId == listing.ProductId)
                    .OrderBy(f => f.Year).ThenBy(f => f.Make).ThenBy(f => f.Model);
                var vehicles = await (compat.MaxCompatibleVehicles > 0 ? fitment.Take(compat.MaxCompatibleVehicles) : fitment)
                    .Select(f => new EbayCompatibleVehicle(f.Year, f.Make, f.Model, f.Trim, f.Engine))
                    .ToListAsync(ct);
                if (vehicles.Count > 0)
                    await _ebay.CreateOrReplaceProductCompatibilityAsync(listing.StoreId, listing.Sku, vehicles, ct);
            }
        }

        var offer = new EbayOffer(
            listing.Sku, account.MarketplaceId, listing.CategoryId, listing.Price, CurrencyFor(account.MarketplaceId),
            listing.Quantity, listing.PaymentPolicyId, listing.FulfillmentPolicyId, listing.ReturnPolicyId,
            account.MerchantLocationKey, listing.Description);

        var now = _clock.UtcNow;
        if (string.IsNullOrEmpty(listing.EbayListingIdExternal))
        {
            // First publish. A previous attempt may have created the offer before publish failed — reuse it.
            if (string.IsNullOrEmpty(listing.OfferId))
                listing.OfferId = await _ebay.CreateOfferAsync(listing.StoreId, offer, ct);
            else
                await _ebay.UpdateOfferAsync(listing.StoreId, listing.OfferId, offer, ct);

            listing.EbayListingIdExternal = await _ebay.PublishOfferAsync(listing.StoreId, listing.OfferId, ct);
            listing.PublishedAtUtc = now;
        }
        else
        {
            await _ebay.UpdateOfferAsync(listing.StoreId, listing.OfferId!, offer, ct);
        }

        listing.Status = EbayListingStatus.Active;
        listing.LastError = null;
        listing.LastSyncedAtUtc = now;
    }

    public async Task EndAsync(EbayListing listing, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(listing.OfferId) && !string.IsNullOrEmpty(listing.EbayListingIdExternal))
            await _ebay.WithdrawOfferAsync(listing.StoreId, listing.OfferId, ct);

        listing.Status = EbayListingStatus.Ended;
        listing.EndedAtUtc = _clock.UtcNow;
        await EbaySupport.SyncChannelFlagAsync(_db, listing.ProductId, listing.Id, ct);
    }

    public async Task<EbayInventorySyncResult> SyncInventoryAsync(
        Guid storeId, IReadOnlyCollection<Guid>? productIds = null, CancellationToken ct = default)
    {
        var account = await _db.EbayAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
        if (account is null || !account.IsEnabled)
            return new EbayInventorySyncResult(0, 0, 0, Array.Empty<string>());

        var query = _db.EbayListings.Where(l => l.StoreId == storeId && l.Status == EbayListingStatus.Active
                                                && l.OfferId != null && l.EbayListingIdExternal != null);
        if (productIds is { Count: > 0 })
            query = query.Where(l => productIds.Contains(l.ProductId));
        var listings = await query.ToListAsync(ct);
        if (listings.Count == 0)
            return new EbayInventorySyncResult(0, 0, 0, Array.Empty<string>());

        var ids = listings.Select(l => l.ProductId).Distinct().ToList();
        var stock = await _db.InventoryLevels.AsNoTracking()
            .Where(l => l.StoreId == storeId && l.ProductVariantId == null && ids.Contains(l.ProductId))
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.StockQuantity) })
            .ToDictionaryAsync(x => x.ProductId, x => Math.Max(0, x.Qty), ct);

        Dictionary<Guid, decimal>? storePrices = null;
        if (account.SyncPriceFromStore)
        {
            var listPrices = await _db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
                .Select(p => new { p.Id, p.Price }).ToListAsync(ct);
            var resolved = await _prices.GetPricesAsync(
                listPrices.Select(p => new GroupPriceRequest(p.Id, null, p.Price ?? 0m)).ToList(), null, storeId, cancellationToken: ct);
            storePrices = resolved.ToDictionary(kv => kv.Key.ProductId, kv => kv.Value.FinalPrice);
        }

        var currency = CurrencyFor(account.MarketplaceId);
        var changed = new List<(EbayListing Listing, int Qty, decimal? Price)>();
        foreach (var listing in listings)
        {
            var qty = stock.GetValueOrDefault(listing.ProductId);
            decimal? price = storePrices is not null && storePrices.TryGetValue(listing.ProductId, out var p) && p > 0 ? p : null;
            if (qty != listing.Quantity || (price is decimal newPrice && newPrice != listing.Price))
                changed.Add((listing, qty, price));
        }

        var updated = 0;
        var failed = 0;
        var errors = new List<string>();
        foreach (var chunk in changed.Chunk(BulkLimit))
        {
            IReadOnlyList<EbayPriceQuantityResult> results;
            try
            {
                results = await _ebay.BulkUpdatePriceQuantityAsync(storeId,
                    chunk.Select(c => new EbayPriceQuantityUpdate(c.Listing.Sku, c.Listing.OfferId, c.Qty, c.Price, currency)).ToList(), ct);
            }
            catch (EbayApiException ex)
            {
                failed += chunk.Length;
                errors.Add(ex.Message);
                _logger.LogWarning("eBay bulk price/quantity update failed for store {StoreId}: {Message}", storeId, ex.Message);
                continue;
            }

            var bySku = results.ToDictionary(r => r.Sku, StringComparer.OrdinalIgnoreCase);
            foreach (var (listing, qty, price) in chunk)
            {
                // A SKU missing from the response is treated as applied (eBay only reports problems on 207).
                if (bySku.TryGetValue(listing.Sku, out var r) && !r.Success)
                {
                    failed++;
                    listing.LastError = EbaySupport.JoinErrors(r.Errors);
                    errors.Add($"{listing.Sku}: {listing.LastError}");
                    continue;
                }

                listing.Quantity = qty;
                if (price is decimal newPrice) listing.Price = newPrice;
                listing.LastSyncedAtUtc = _clock.UtcNow;
                updated++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new EbayInventorySyncResult(listings.Count, updated, failed, errors);
    }

    public async Task<int> EndOutOfStockAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0) return 0;
        var ids = productIds.Distinct().ToList();

        // The products themselves plus every bundle (Grouped product) that contains one of them.
        var bundleIds = await _db.GroupedProductMembers.AsNoTracking()
            .Where(m => ids.Contains(m.MemberProductId))
            .Select(m => m.GroupedProductId)
            .Distinct()
            .ToListAsync(ct);

        var candidates = await _db.EbayListings
            .Where(l => l.Status == EbayListingStatus.Active && (ids.Contains(l.ProductId) || bundleIds.Contains(l.ProductId)))
            .ToListAsync(ct);

        var ended = 0;
        foreach (var listing in candidates)
        {
            // A bundle is ended when any of its out-of-stock members triggered this; a product only when
            // its own store stock really is 0 (the trigger may be stale by the time the job runs).
            if (ids.Contains(listing.ProductId))
            {
                var qty = await EbaySupport.StoreQuantity(_db, listing.StoreId, listing.ProductId).SumAsync(ct);
                if (qty > 0) continue;
            }
            else
            {
                var members = await _db.GroupedProductMembers.AsNoTracking()
                    .Where(m => m.GroupedProductId == listing.ProductId && ids.Contains(m.MemberProductId))
                    .Select(m => m.MemberProductId).ToListAsync(ct);
                var anyEmpty = false;
                foreach (var member in members)
                    anyEmpty |= await EbaySupport.StoreQuantity(_db, listing.StoreId, member).SumAsync(ct) <= 0;
                if (!anyEmpty) continue;
            }

            try
            {
                await EndAsync(listing, ct);
                ended++;
            }
            catch (EbayApiException ex)
            {
                listing.LastError = EbaySupport.JoinErrors(ex.Errors);
                _logger.LogWarning("Could not end out-of-stock eBay listing {ListingId}: {Message}", listing.Id, ex.Message);
            }
            catch (ServiceUnavailableException ex)
            {
                _logger.LogWarning("Could not end out-of-stock eBay listing {ListingId}: {Message}", listing.Id, ex.Message);
            }
        }

        await _db.SaveChangesAsync(ct);
        return ended;
    }
}
