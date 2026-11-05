using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Ebay;

/// <inheritdoc cref="IEbayMetadataService"/>
public sealed class EbayMetadataService : IEbayMetadataService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IEbayClient _ebay;
    private readonly IMemoryCache _cache;
    private readonly IApplicationDbContext _db;

    public EbayMetadataService(IEbayClient ebay, IMemoryCache cache, IApplicationDbContext db)
    {
        _ebay = ebay;
        _cache = cache;
        _db = db;
    }

    public Task<IReadOnlyList<EbayPolicy>> GetPoliciesAsync(Guid storeId, EbayPolicyType type, bool refresh = false, CancellationToken ct = default) =>
        CachedAsync($"ebay:policies:{storeId}:{type}", refresh, () => _ebay.GetPoliciesAsync(storeId, type, ct));

    public async Task<IReadOnlyList<EbayAspectDefinition>> GetAspectsAsync(Guid storeId, string categoryId, CancellationToken ct = default)
    {
        var treeId = await TreeIdAsync(storeId, ct);
        return await CachedAsync($"ebay:aspects:{storeId}:{treeId}:{categoryId}", false,
            () => _ebay.GetItemAspectsAsync(storeId, treeId, categoryId, ct));
    }

    public Task<IReadOnlyList<EbayCondition>> GetConditionsAsync(Guid storeId, string categoryId, CancellationToken ct = default) =>
        CachedAsync($"ebay:conditions:{storeId}:{categoryId}", false, () => _ebay.GetItemConditionsAsync(storeId, categoryId, ct));

    public Task<EbayCompatibilityInfo> GetCompatibilityAsync(Guid storeId, string categoryId, CancellationToken ct = default) =>
        CachedAsync($"ebay:compat:{storeId}:{categoryId}", false, () => _ebay.GetCompatibilityInfoAsync(storeId, categoryId, ct));

    private async Task<string> TreeIdAsync(Guid storeId, CancellationToken ct)
    {
        var treeId = await _db.EbayAccounts.AsNoTracking()
            .Where(a => a.StoreId == storeId).Select(a => a.CategoryTreeId).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrEmpty(treeId))
            return treeId;

        treeId = await CachedAsync($"ebay:tree:{storeId}", false, () => _ebay.GetDefaultCategoryTreeIdAsync(storeId, ct));
        await _db.EbayAccounts.Where(a => a.StoreId == storeId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CategoryTreeId, treeId), ct);
        return treeId;
    }

    private async Task<T> CachedAsync<T>(string key, bool refresh, Func<Task<T>> load)
    {
        if (!refresh && _cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        // Failures are not cached, so a transient eBay error is retried on the next request.
        var value = await load();
        _cache.Set(key, value, Ttl);
        return value;
    }
}
