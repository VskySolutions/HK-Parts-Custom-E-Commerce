using System.Collections.Concurrent;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks;

/// <summary>Thread-safe in-memory record of each task's last/next run, surfaced to the admin API.</summary>
public class TaskScheduleRegistry : IBackgroundTaskStatusProvider
{
    private readonly ConcurrentDictionary<string, BackgroundTaskStatus> _statuses = new();

    public void Register(string name) => _statuses.TryAdd(name, new BackgroundTaskStatus { TaskName = name });

    public void SetNextRun(string name, DateTime nextRunUtc) =>
        Update(name, s => s.NextRunUtc = nextRunUtc);

    public void MarkStarted(string name, DateTime startedUtc) =>
        Update(name, s =>
        {
            s.LastStartedUtc = startedUtc;
            s.LastStatus = "Running";
        });

    public void MarkCompleted(string name, DateTime completedUtc, string status, string? error) =>
        Update(name, s =>
        {
            s.LastCompletedUtc = completedUtc;
            s.LastStatus = status;
            s.LastError = error;
        });

    public IReadOnlyList<BackgroundTaskStatus> GetStatuses() =>
        _statuses.Values.OrderBy(s => s.TaskName).Select(Clone).ToList();

    private void Update(string name, Action<BackgroundTaskStatus> update) =>
        _statuses.AddOrUpdate(
            name,
            _ => { var s = new BackgroundTaskStatus { TaskName = name }; update(s); return s; },
            (_, s) => { update(s); return s; });

    private static BackgroundTaskStatus Clone(BackgroundTaskStatus s) => new()
    {
        TaskName = s.TaskName,
        LastStartedUtc = s.LastStartedUtc,
        LastCompletedUtc = s.LastCompletedUtc,
        LastStatus = s.LastStatus,
        LastError = s.LastError,
        NextRunUtc = s.NextRunUtc,
    };
}
