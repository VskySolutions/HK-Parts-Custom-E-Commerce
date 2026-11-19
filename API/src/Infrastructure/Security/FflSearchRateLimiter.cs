using Microsoft.Extensions.Caching.Memory;

namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>
/// FFL-132: fixed-window limit on dealer radius searches, 30 per minute per client IP (checkout and the public
/// finder share the budget). In-memory: per app instance.
/// </summary>
public sealed class FflSearchRateLimiter
{
    public const int MaxSearches = 30;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _cache;

    public FflSearchRateLimiter(IMemoryCache cache) => _cache = cache;

    /// <summary>Records one search; false when the caller is over the limit.</summary>
    public bool TryConsume(string? ip)
    {
        var counter = _cache.GetOrCreate("ffl-search:ip:" + (string.IsNullOrWhiteSpace(ip) ? "unknown" : ip), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new Counter();
        })!;
        return Interlocked.Increment(ref counter.Value) <= MaxSearches;
    }

    private sealed class Counter
    {
        public int Value;
    }
}
