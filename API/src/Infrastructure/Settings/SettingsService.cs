using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Settings;

/// <summary>Cached settings accessor. Reads are memoized; writes audit and invalidate the cache.</summary>
public class SettingsService : ISettingsService
{
    private const string CachePrefix = "setting:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;

    public SettingsService(IApplicationDbContext db, IMemoryCache cache, ICurrentUserService current, IDateTimeProvider clock)
    {
        _db = db;
        _cache = cache;
        _current = current;
        _clock = clock;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CachePrefix + key, out string? cached))
            return cached;

        var setting = await _db.PlatformSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        _cache.Set(CachePrefix + key, setting?.Value, CacheTtl);
        return setting?.Value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var raw = await GetValueAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(raw))
            return default;

        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        try
        {
            return (T)Convert.ChangeType(raw, target);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return default;
        }
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            setting = new PlatformSetting { Key = key, ValueType = "string" };
            _db.PlatformSettings.Add(setting);
        }

        var previous = setting.Value;
        if (previous == value)
            return;

        setting.Value = value;

        _db.SettingsChangeHistory.Add(new SettingsChangeHistory
        {
            SettingKey = key,
            PreviousValue = previous,
            NewValue = value,
            ChangedOnUtc = _clock.UtcNow,
            ActorId = _current.UserId,
            ActorName = _current.Email,
        });

        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove(CachePrefix + key);
    }
}
