using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>Exposes the scheduler's per-task last/next run status to the admin dashboard.</summary>
public interface IBackgroundTaskStatusProvider
{
    IReadOnlyList<BackgroundTaskStatus> GetStatuses();
}
