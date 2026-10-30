using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Generates recurring subscription orders on schedule (REQ-ORD-005, AC-ORD-005.3). Delegates to
/// <see cref="ISubscriptionService.GenerateDueOrdersAsync"/>, which creates a Pending order for each due
/// Active subscription and advances its next-order date; a per-subscription failure pauses that
/// subscription and notifies the subscriber, so re-running is safe (a paused sub is skipped next time).
/// </summary>
public class SubscriptionOrderWorker : IScheduledTask
{
    public string Name => "SubscriptionOrderWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromHours(24);
    public string? IntervalSettingKey => "tasks.subscription-orders.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);
        var subscriptions = services.GetRequiredService<ISubscriptionService>();

        var generated = await subscriptions.GenerateDueOrdersAsync(cancellationToken);
        if (generated > 0)
            logger.LogInformation("{Task} generated {Count} recurring subscription order(s).", Name, generated);
    }
}
