using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Periodically flags payment authorizations that lapsed before capture (WO-34, AC-PAY-002.4). Delegates
/// to <see cref="IExpiredAuthorizationScanner"/>, which voids each expired hold and raises an admin alert.
/// The scan is idempotent (voiding removes the hold from the next run), so re-running is safe.
/// </summary>
public class ExpiredAuthorizationWorker : IScheduledTask
{
    public string Name => "ExpiredAuthorizationWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromHours(1);
    public string? IntervalSettingKey => "tasks.expired-auth-scan.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);
        var scanner = services.GetRequiredService<IExpiredAuthorizationScanner>();

        var voided = await scanner.ScanAsync(cancellationToken);
        if (voided > 0)
            logger.LogInformation("{Task} voided and flagged {Count} expired payment authorization(s).", Name, voided);
    }
}
