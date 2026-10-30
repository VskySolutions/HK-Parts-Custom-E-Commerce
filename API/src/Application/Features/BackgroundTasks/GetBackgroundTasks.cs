using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.BackgroundTasks;

public record GetBackgroundTasksQuery : IRequest<IReadOnlyList<BackgroundTaskStatus>>;

public class GetBackgroundTasksQueryHandler
    : IRequestHandler<GetBackgroundTasksQuery, IReadOnlyList<BackgroundTaskStatus>>
{
    private readonly IBackgroundTaskStatusProvider _provider;

    public GetBackgroundTasksQueryHandler(IBackgroundTaskStatusProvider provider) => _provider = provider;

    public Task<IReadOnlyList<BackgroundTaskStatus>> Handle(GetBackgroundTasksQuery request, CancellationToken cancellationToken)
        => Task.FromResult(_provider.GetStatuses());
}
