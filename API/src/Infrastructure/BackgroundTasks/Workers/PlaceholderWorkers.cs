using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

// These workers are wired into the scheduler now (schedule + execution logging satisfied); their
// domain logic lands with the features they depend on (inventory — later phases).
// AbandonedCartWorker's real implementation lives in AbandonedCartWorker.cs (WO-31).

/// <summary>Generates low-stock alerts (inventory feature — later phase).</summary>
public class LowStockAlertWorker : IScheduledTask
{
    public string Name => "LowStockAlertWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(120);
    public string? IntervalSettingKey => "tasks.low-stock.interval-minutes";
    public string? CronSettingKey => null;

    public Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        services.GetRequiredService<ILoggerFactory>().CreateLogger(Name)
            .LogInformation("{Task} ran; inventory feature not yet available, nothing to process.", Name);
        return Task.CompletedTask;
    }
}
