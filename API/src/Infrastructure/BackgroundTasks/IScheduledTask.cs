namespace HKPartsEcommerce.Infrastructure.BackgroundTasks;

/// <summary>
/// A recurring background task run by <see cref="TaskSchedulerService"/>. Implementations are
/// stateless singletons that resolve scoped services from the provided scope per run.
/// </summary>
public interface IScheduledTask
{
    string Name { get; }

    /// <summary>Interval used when no settings key is configured (or the setting is invalid).</summary>
    TimeSpan DefaultInterval { get; }

    /// <summary>Optional "…interval-minutes" setting key that overrides <see cref="DefaultInterval"/>.</summary>
    string? IntervalSettingKey { get; }

    /// <summary>Optional settings key holding a cron expression; when present it takes precedence over the interval.</summary>
    string? CronSettingKey { get; }

    Task RunAsync(IServiceProvider services, CancellationToken cancellationToken);
}
