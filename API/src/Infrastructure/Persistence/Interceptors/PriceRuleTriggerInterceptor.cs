using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Keeps the pre-calculated price-rule table in step with the catalog without every handler having to
/// remember to: after a successful save that changed a product's price/type/deletion, a variant price, a
/// category or store assignment, or a group price, it queues a rebuild of the rules touching those products.
/// A change to a customer group's own pricing (or a very large batch, i.e. an import) queues a full rebuild.
/// Nothing is queued when the save fails.
/// </summary>
public sealed class PriceRuleTriggerInterceptor : SaveChangesInterceptor
{
    /// <summary>Beyond this many products in one save (a bulk import), rebuilding everything is cheaper.</summary>
    private const int FullRebuildThreshold = 1000;

    private readonly IPriceRuleRebuildQueue _queue;
    private readonly HashSet<Guid> _products = new();
    private bool _all;

    public PriceRuleTriggerInterceptor(IPriceRuleRebuildQueue queue) => _queue = queue;

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

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
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
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            switch (entry.Entity)
            {
                case Product p when Touches(entry, nameof(Product.Price), nameof(Product.ProductType), nameof(Product.Deleted)):
                    _products.Add(p.Id);
                    break;
                case ProductVariant v when Touches(entry, nameof(ProductVariant.Price), nameof(ProductVariant.Deleted)):
                    _products.Add(v.ProductId);
                    break;
                case ProductCategory pc when Touches(entry, nameof(ProductCategory.CategoryId), nameof(ProductCategory.ProductId)):
                    _products.Add(pc.ProductId);
                    break;
                case ProductStoreMapping sm when Touches(entry, nameof(ProductStoreMapping.StoreId)):
                    _products.Add(sm.ProductId);
                    break;
                case CustomerGroupPrice gp:
                    _products.Add(gp.ProductId);
                    break;
                case CustomerGroup when Touches(entry,
                    nameof(CustomerGroup.PricingRuleType), nameof(CustomerGroup.DiscountPercent),
                    nameof(CustomerGroup.IsActive), nameof(CustomerGroup.Deleted)):
                    _all = true;
                    break;
            }
        }
    }

    /// <summary>Added/Deleted always count; a modification counts only when one of the named columns changed.</summary>
    private static bool Touches(EntityEntry entry, params string[] properties) =>
        entry.State != EntityState.Modified || properties.Any(name => entry.Property(name).IsModified);

    private void Flush()
    {
        if (_all || _products.Count > FullRebuildThreshold)
            _queue.EnqueueAll();
        else if (_products.Count > 0)
            _queue.EnqueueForProducts(_products);
        Reset();
    }

    private void Reset()
    {
        _all = false;
        _products.Clear();
    }
}
