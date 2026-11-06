using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The "domain events" that drive eBay jobs, raised after a successful save:
/// <list type="bullet">
/// <item>a product's store stock changed → inventory sync for that store/product; reached 0 → out-of-stock end;</item>
/// <item>a product's price changed → inventory sync in every store (only pushes when SyncPriceFromStore is on);</item>
/// <item>a shipment got a carrier + tracking number → shipment sync (a no-op for non-eBay orders).</item>
/// </list>
/// </summary>
public sealed class EbayTriggerInterceptor : SaveChangesInterceptor
{
    private readonly IEbayJobQueue _jobs;
    private readonly Dictionary<Guid, HashSet<Guid>> _stockChanges = new();
    private readonly HashSet<Guid> _outOfStock = new();
    private readonly HashSet<Guid> _priceChanges = new();
    private readonly HashSet<Guid> _shipments = new();

    public EbayTriggerInterceptor(IEbayJobQueue jobs) => _jobs = jobs;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Reset();

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Reset();
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            switch (entry.Entity)
            {
                case InventoryLevel level when level.ProductVariantId is null
                                               && Touches(entry, nameof(InventoryLevel.StockQuantity)):
                    if (!_stockChanges.TryGetValue(level.StoreId, out var products))
                        _stockChanges[level.StoreId] = products = new HashSet<Guid>();
                    products.Add(level.ProductId);
                    if (level.StockQuantity <= 0)
                        _outOfStock.Add(level.ProductId);
                    break;

                case Product product when entry.State == EntityState.Modified && Touches(entry, nameof(Product.Price), nameof(Product.SalePrice)):
                    _priceChanges.Add(product.Id);
                    break;

                case Shipment shipment when string.IsNullOrEmpty(shipment.EbayFulfillmentId)
                                            && !string.IsNullOrWhiteSpace(shipment.TrackingNumber)
                                            && !string.IsNullOrWhiteSpace(shipment.Carrier)
                                            && Touches(entry, nameof(Shipment.TrackingNumber), nameof(Shipment.Carrier)):
                    _shipments.Add(shipment.Id);
                    break;
            }
        }
    }

    private static bool Touches(EntityEntry entry, params string[] properties) =>
        entry.State == EntityState.Added || properties.Any(name => entry.Property(name).IsModified);

    private void Flush()
    {
        if (_outOfStock.Count > 0)
            _jobs.EnqueueOutOfStock(_outOfStock);
        foreach (var (storeId, products) in _stockChanges)
            _jobs.EnqueueInventorySync(storeId, products);
        if (_priceChanges.Count > 0)
            _jobs.EnqueueInventorySync(null, _priceChanges);
        foreach (var shipmentId in _shipments)
            _jobs.EnqueueShipmentSync(shipmentId);
        Reset();
    }

    private void Reset()
    {
        _stockChanges.Clear();
        _outOfStock.Clear();
        _priceChanges.Clear();
        _shipments.Clear();
    }
}
