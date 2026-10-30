using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks;

/// <summary>
/// Hosted coordinator that runs each <see cref="IScheduledTask"/> on its configured interval, isolates
/// failures per task, and logs every execution to <c>BackgroundTaskLog</c> (Background Task Scheduler ADR-001).
/// </summary>
public class TaskSchedulerService : BackgroundService
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(30);

    private readonly IReadOnlyList<IScheduledTask> _tasks;
    private readonly IServiceProvider _services;
    private readonly TaskScheduleRegistry _registry;
    private readonly ILogger<TaskSchedulerService> _logger;

    public TaskSchedulerService(
        IEnumerable<IScheduledTask> tasks,
        IServiceProvider services,
        TaskScheduleRegistry registry,
        ILogger<TaskSchedulerService> logger)
    {
        _tasks = tasks.ToList();
        _services = services;
        _registry = registry;
        _logger = logger;

        foreach (var task in _tasks)
            _registry.Register(task.Name);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task scheduler starting with {Count} task(s).", _tasks.Count);
        await Task.WhenAll(_tasks.Select(t => RunLoopAsync(t, stoppingToken)));
    }

    private async Task RunLoopAsync(IScheduledTask task, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = await ComputeNextRunAsync(task, stoppingToken);
            _registry.SetNextRun(task.Name, nextRun);

            var delay = nextRun - DateTime.UtcNow;
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.FromSeconds(1);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ExecuteOnceAsync(task, stoppingToken);
        }
    }

    private async Task<DateTime> ComputeNextRunAsync(IScheduledTask task, CancellationToken ct)
    {
        // A cron expression (from DB settings) takes precedence over a fixed interval.
        if (task.CronSettingKey is not null)
        {
            var cron = await ReadSettingAsync(task.CronSettingKey, ct);
            if (!string.IsNullOrWhiteSpace(cron))
            {
                try
                {
                    var next = CronExpression.Parse(cron).GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
                    if (next.HasValue)
                        return next.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid cron '{Cron}' for {Task}; falling back to interval.", cron, task.Name);
                }
            }
        }

        return DateTime.UtcNow.Add(await ResolveIntervalAsync(task, ct));
    }

    private async Task<string?> ReadSettingAsync(string key, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        return await settings.GetValueAsync(key, ct);
    }

    private async Task<TimeSpan> ResolveIntervalAsync(IScheduledTask task, CancellationToken ct)
    {
        var interval = task.DefaultInterval;
        if (task.IntervalSettingKey is not null)
        {
            try
            {
                using var scope = _services.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var minutes = await settings.GetAsync<int>(task.IntervalSettingKey, ct);
                if (minutes > 0)
                    interval = TimeSpan.FromMinutes(minutes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falling back to the default interval for {Task}.", task.Name);
            }
        }

        return interval < MinimumInterval ? MinimumInterval : interval;
    }

    private async Task ExecuteOnceAsync(IScheduledTask task, CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        _registry.MarkStarted(task.Name, startedAt);

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var log = new BackgroundTaskLog { TaskName = task.Name, StartedOnUtc = startedAt, Status = "Running" };
        db.BackgroundTaskLogs.Add(log);
        await db.SaveChangesAsync(ct);

        try
        {
            await task.RunAsync(scope.ServiceProvider, ct);

            log.Status = "Succeeded";
            log.CompletedOnUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _registry.MarkCompleted(task.Name, log.CompletedOnUtc.Value, "Succeeded", null);
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.CompletedOnUtc = DateTime.UtcNow;
            log.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            _registry.MarkCompleted(task.Name, log.CompletedOnUtc.Value, "Failed", ex.Message);
            _logger.LogError(ex, "Background task {Task} failed.", task.Name);
        }
    }
}
