using MediatR;
using HKPartsEcommerce.Application.Common.Authorization;

namespace HKPartsEcommerce.Application.Features.Roles;

/// <summary>Returns the catalog of admin modules so an admin UI can populate the role editor.</summary>
public record GetModulesQuery : IRequest<IReadOnlyList<ModuleInfo>>;

public class GetModulesQueryHandler : IRequestHandler<GetModulesQuery, IReadOnlyList<ModuleInfo>>
{
    public Task<IReadOnlyList<ModuleInfo>> Handle(GetModulesQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Modules.All);
}
