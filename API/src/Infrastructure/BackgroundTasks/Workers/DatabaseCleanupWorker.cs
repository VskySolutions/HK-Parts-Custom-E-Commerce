using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Daily cleanup: purges expired/long-revoked refresh tokens, log rows past the retention window, and
/// old background-task logs (Background Task Scheduler blueprint). Fully functional now.
/// </summary>
public class DatabaseCleanupWorker : IScheduledTask
{
    public string Name => "DatabaseCleanupWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromHours(24);
    public string? IntervalSettingKey => null;
    public string? CronSettingKey => "tasks.db-cleanup.cron"; // e.g. "0 3 * * *" (daily at 03:00 UTC)

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var settings = services.GetRequiredService<ISettingsService>();
        var now = DateTime.UtcNow;

        // Expired refresh tokens, and tokens revoked more than a week ago.
        var revokedCutoff = now.AddDays(-7);
        await db.RefreshTokens
            .Where(r => r.ExpiresOnUtc < now || (r.RevokedOnUtc != null && r.RevokedOnUtc < revokedCutoff))
            .ExecuteDeleteAsync(cancellationToken);

        var retentionDays = await settings.GetAsync<int>("logging.retention-days", cancellationToken);
        if (retentionDays <= 0)
            retentionDays = 90;
        var logCutoff = now.AddDays(-retentionDays);

        await db.ApplicationLogs
            .Where(l => l.TimeStamp < logCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        await db.BackgroundTaskLogs
            .Where(l => l.StartedOnUtc < logCutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
