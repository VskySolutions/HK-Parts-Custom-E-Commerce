namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Read/write access to DB-backed platform settings with caching. Writes record change history and
/// invalidate the cache so new values apply on the next request without a restart (Platform Foundation, ADR-001).
/// </summary>
public interface ISettingsService
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
}
