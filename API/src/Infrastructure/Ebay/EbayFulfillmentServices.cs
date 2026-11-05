using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Ebay;

/// <summary>
/// Reports a shipment's tracking number back to eBay (createShippingFulfillment, all eBay line items of the
/// order). Runs once per shipment: the returned fulfillment id is stored on the shipment and a shipment that
/// already has one is skipped.
/// </summary>
public sealed class EbayShipmentSyncService
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayClient _ebay;
    private readonly ILogger<EbayShipmentSyncService> _logger;

    public EbayShipmentSyncService(IApplicationDbContext db, IEbayClient ebay, ILogger<EbayShipmentSyncService> logger)
    {
        _db = db;
        _ebay = ebay;
        _logger = logger;
    }

    /// <returns>True when eBay was called (and accepted it).</returns>
    public async Task<bool> SyncAsync(Guid shipmentId, CancellationToken ct = default)
    {
        var shipment = await _db.Shipments.Include(s => s.Order).ThenInclude(o => o!.Lines)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, ct);
        var order = shipment?.Order;
        if (shipment is null || order is null || string.IsNullOrEmpty(order.EbayOrderId)
            || !string.IsNullOrEmpty(shipment.EbayFulfillmentId)
            || string.IsNullOrWhiteSpace(shipment.TrackingNumber) || string.IsNullOrWhiteSpace(shipment.Carrier)
            || order.AssignedStoreId is not Guid storeId)
            return false;

        var lines = order.Lines
            .Where(l => !string.IsNullOrEmpty(l.EbayLineItemId))
            .Select(l => (l.EbayLineItemId!, l.Quantity))
            .ToList();
        if (lines.Count == 0)
            return false;

        shipment.EbayFulfillmentId = await _ebay.CreateShippingFulfillmentAsync(
            storeId, order.EbayOrderId, lines, CarrierCode(shipment.Carrier), shipment.TrackingNumber.Trim(), ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Reported tracking {Tracking} for eBay order {EbayOrderId}.", shipment.TrackingNumber, order.EbayOrderId);
        return true;
    }

    /// <summary>Store carrier names → eBay shippingCarrierCode.</summary>
    public static string CarrierCode(string carrier)
    {
        var key = new string(carrier.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return key switch
        {
            "USPS" or "UNITEDSTATESPOSTALSERVICE" => "USPS",
            "UPS" or "UNITEDPARCELSERVICE" => "UPS",
            "FEDEX" or "FEDERALEXPRESS" => "FEDEX",
            var k when k.StartsWith("DHL", StringComparison.Ordinal) => "DHL",
            "CANADAPOST" => "CANADA_POST",
            "ROYALMAIL" => "ROYAL_MAIL",
            _ => key,
        };
    }
}

/// <summary>Downloads the marketplace category tree (Taxonomy) into <c>EbayCategories</c>, replacing the old rows.</summary>
public sealed class EbayCategoryRefreshService
{
    public const string LastRefreshedSettingKey = "ebay.categories.last-refreshed-utc";
    private const int BatchSize = 2000;

    private readonly IApplicationDbContext _db;
    private readonly IEbayClient _ebay;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<EbayCategoryRefreshService> _logger;

    public EbayCategoryRefreshService(
        IApplicationDbContext db, IEbayClient ebay, ISettingsService settings, IDateTimeProvider clock,
        ILogger<EbayCategoryRefreshService> logger)
    {
        _db = db;
        _ebay = ebay;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> RefreshAsync(Guid storeId, CancellationToken ct = default)
    {
        var treeId = await _ebay.GetDefaultCategoryTreeIdAsync(storeId, ct);
        var tree = await _ebay.GetCategoryTreeAsync(storeId, treeId, ct);
        if (tree.Categories.Count == 0)
            throw new InvalidOperationException("eBay returned an empty category tree; keeping the existing categories.");

        var ownsTransaction = _db.Database.CurrentTransaction is null;
        var tx = ownsTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await _db.EbayCategories.ExecuteDeleteAsync(ct);
            foreach (var batch in tree.Categories.Chunk(BatchSize))
            {
                _db.EbayCategories.AddRange(batch.Select(c => new EbayCategory
                {
                    CategoryId = c.CategoryId,
                    ParentCategoryId = c.ParentCategoryId,
                    Name = c.Name.Length > 200 ? c.Name[..200] : c.Name,
                    Level = c.Level,
                    IsLeaf = c.IsLeaf,
                    TreeVersion = tree.Version,
                }));
                await _db.SaveChangesAsync(ct);
                if (_db is DbContext context) context.ChangeTracker.Clear();
            }

            await _db.EbayAccounts.Where(a => a.StoreId == storeId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.CategoryTreeId, tree.TreeId), ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        await _settings.SetAsync(LastRefreshedSettingKey, _clock.UtcNow.ToString("O"), ct);
        _logger.LogInformation("Loaded {Count} eBay categories (tree {TreeId} v{Version}).", tree.Categories.Count, tree.TreeId, tree.Version);
        return tree.Categories.Count;
    }
}
