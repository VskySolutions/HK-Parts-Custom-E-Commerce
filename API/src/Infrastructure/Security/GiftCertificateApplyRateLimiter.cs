using Microsoft.Extensions.Caching.Memory;

namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>
/// Fixed-window limiter for gift certificate apply attempts: 10 per 15 minutes, counted separately per client
/// IP and per cart. Every attempt counts (successful ones too), so a code cannot be brute-forced by mixing in
/// valid applies. In-memory: per app instance.
/// </summary>
public sealed class GiftCertificateApplyRateLimiter
{
    public const int MaxAttempts = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public GiftCertificateApplyRateLimiter(IMemoryCache cache) => _cache = cache;

    /// <summary>Records one attempt; true when it exceeds the limit for the IP or the cart.</summary>
    public bool TryConsume(string? ip, string? cartKey)
    {
        // Count both keys even when the first is already over, so neither window can be dodged.
        var ipLimited = Exceeded("gc-apply:ip:" + (string.IsNullOrWhiteSpace(ip) ? "unknown" : ip));
        var cartLimited = !string.IsNullOrWhiteSpace(cartKey) && Exceeded("gc-apply:cart:" + cartKey);
        return !(ipLimited || cartLimited);
    }

    private bool Exceeded(string key)
    {
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new Counter();
        })!;
        return Interlocked.Increment(ref counter.Value) > MaxAttempts;
    }

    private sealed class Counter
    {
        public int Value;
    }
}
