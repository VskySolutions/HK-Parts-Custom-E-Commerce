using Microsoft.Extensions.Caching.Memory;

namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>
/// SS-53: <c>/shipstation</c> takes at most 60 requests a minute per client IP, and an IP is locked out for
/// 15 minutes after 10 failed logins. In-memory: per app instance.
/// </summary>
public sealed class ShipStationRateLimiter
{
    public const int MaxRequestsPerMinute = 60;
    public const int MaxFailedLogins = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public ShipStationRateLimiter(IMemoryCache cache) => _cache = cache;

    private static string Ip(string? ip) => string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;

    /// <summary>Records one request; false when the IP is over the per-minute limit.</summary>
    public bool TryConsume(string? ip)
    {
        var counter = _cache.GetOrCreate("shipstation:rate:" + Ip(ip), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new Counter();
        })!;
        return Interlocked.Increment(ref counter.Value) <= MaxRequestsPerMinute;
    }

    public bool IsLockedOut(string? ip) => _cache.TryGetValue("shipstation:locked:" + Ip(ip), out _);

    /// <summary>Counts a failed login; the tenth within the lockout window locks the IP out.</summary>
    public void RecordFailure(string? ip)
    {
        var counter = _cache.GetOrCreate("shipstation:fail:" + Ip(ip), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Lockout;
            return new Counter();
        })!;
        if (Interlocked.Increment(ref counter.Value) >= MaxFailedLogins)
        {
            _cache.Set("shipstation:locked:" + Ip(ip), true, Lockout);
            _cache.Remove("shipstation:fail:" + Ip(ip));
        }
    }

    public void RecordSuccess(string? ip) => _cache.Remove("shipstation:fail:" + Ip(ip));

    private sealed class Counter
    {
        public int Value;
    }
}
