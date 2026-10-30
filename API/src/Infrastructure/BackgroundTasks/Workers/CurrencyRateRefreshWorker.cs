using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Refreshes currency exchange rates from the configured external source on schedule (WO-90), skipping
/// locked and base currencies. Relocated out of TrackingSyncWorker when that worker took on real carrier
/// tracking sync (WO-42). No-op unless auto-refresh is enabled and a source is configured.
/// </summary>
public class CurrencyRateRefreshWorker : IScheduledTask
{
    public string Name => "CurrencyRateRefreshWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(180);
    public string? IntervalSettingKey => "tasks.currency-refresh.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);
        var refresher = services.GetRequiredService<ICurrencyRateRefresher>();

        var updated = await refresher.RefreshAsync(cancellationToken);
        if (updated > 0)
            logger.LogInformation("{Task} refreshed {Count} currency exchange rate(s).", Name, updated);
    }
}
