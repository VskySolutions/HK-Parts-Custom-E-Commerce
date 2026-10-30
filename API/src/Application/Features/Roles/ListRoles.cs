using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Roles;

public record ListRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public class ListRolesQueryHandler : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IApplicationDbContext _db;

    public ListRolesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(RoleDto.From).ToList();
    }
}
