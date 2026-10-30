using Microsoft.Extensions.Caching.Memory;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Storage;

/// <summary>
/// In-memory <see cref="IMediaTempStore"/> (WO-122): prepared uploads live in the memory cache under an
/// opaque id and expire after a fixed TTL, so an abandoned prepare never leaks. Single-instance scope is
/// acceptable — a prepared upload is committed by the same admin within minutes.
/// </summary>
public class MediaTempStore : IMediaTempStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private const string Prefix = "media-temp:";

    private readonly IMemoryCache _cache;

    public MediaTempStore(IMemoryCache cache) => _cache = cache;

    public string Store(MediaTempEntry entry)
    {
        var tempId = Guid.NewGuid().ToString("N");
        _cache.Set(Prefix + tempId, entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
        });
        return tempId;
    }

    public MediaTempEntry? Get(string tempId)
        => string.IsNullOrWhiteSpace(tempId) ? null : _cache.Get<MediaTempEntry>(Prefix + tempId);

    public void Remove(string tempId)
    {
        if (!string.IsNullOrWhiteSpace(tempId))
            _cache.Remove(Prefix + tempId);
    }
}
