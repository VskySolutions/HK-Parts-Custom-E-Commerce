using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Currencies;

/// <summary>
/// Pulls exchange rates from the configured provider and updates enabled, non-base, unlocked currencies.
/// Rate-locked currencies are intentionally skipped (WO-90 locked-rate requirement). Network/parse
/// failures are logged as warnings and swallowed so a scheduled run never throws.
/// </summary>
public class CurrencyRateRefresher : ICurrencyRateRefresher
{
    private const string LastRunKey = "currency.auto-refresh.last-run-utc";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CurrencyRateRefresher> _logger;

    public CurrencyRateRefresher(
        IApplicationDbContext db,
        ISettingsService settings,
        IHttpClientFactory httpClientFactory,
        ILogger<CurrencyRateRefresher> logger)
    {
        _db = db;
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var enabled = await _settings.GetAsync<bool>("currency.auto-refresh.enabled", cancellationToken);
        if (enabled is not true)
            return 0;

        var sourceUrl = await _settings.GetValueAsync("currency.auto-refresh.source-url", cancellationToken);
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return 0;

        // Honor the configured cadence: skip if the last refresh is more recent than interval-hours.
        var intervalHours = await _settings.GetAsync<int>("currency.auto-refresh.interval-hours", cancellationToken);
        if (intervalHours <= 0)
            intervalHours = 24;

        var lastRunRaw = (await _db.PlatformSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == LastRunKey, cancellationToken))?.Value;
        if (DateTime.TryParse(lastRunRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastRun)
            && DateTime.UtcNow - lastRun < TimeSpan.FromHours(intervalHours))
        {
            return 0;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("currency-rate-refresh");
            client.Timeout = HttpTimeout;

            using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("rates", out var rates) || rates.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Currency rate refresh: response from {SourceUrl} contained no 'rates' object.", sourceUrl);
                return 0;
            }

            var currencies = await _db.SupportedCurrencies
                .Where(c => c.IsEnabled && !c.IsBaseCurrency && !c.IsRateLocked)
                .ToListAsync(cancellationToken);

            var updated = 0;
            foreach (var currency in currencies)
            {
                if (rates.TryGetProperty(currency.CurrencyCode, out var rateElement)
                    && rateElement.ValueKind == JsonValueKind.Number
                    && rateElement.TryGetDecimal(out var rate))
                {
                    currency.ExchangeRate = rate;
                    currency.LastRateUpdatedOnUtc = DateTime.UtcNow;
                    updated++;
                }
            }

            await MarkRefreshedAsync(cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Currency rate refresh updated {Count} rate(s) from {SourceUrl}.", updated, sourceUrl);
            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Currency rate refresh from {SourceUrl} failed; no rates were updated.", sourceUrl);
            return 0;
        }
    }

    // Records the last-run timestamp directly (bypassing the settings service so it stays out of the
    // change-history/cache). Saved by the caller's SaveChangesAsync.
    private async Task MarkRefreshedAsync(CancellationToken ct)
    {
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == LastRunKey, ct);
        if (setting is null)
        {
            setting = new PlatformSetting
            {
                Key = LastRunKey,
                ValueType = "string",
                Category = "Currency",
                Description = "Timestamp (UTC) of the last successful exchange-rate refresh.",
            };
            _db.PlatformSettings.Add(setting);
        }
        setting.Value = DateTime.UtcNow.ToString("O");
    }
}
