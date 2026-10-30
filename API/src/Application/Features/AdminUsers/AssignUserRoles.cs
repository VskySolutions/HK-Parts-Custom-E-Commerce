using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.AdminUsers;

/// <summary>Replaces a user's role assignments with exactly the requested set of roles.</summary>
public record AssignUserRolesCommand(Guid UserId, List<Guid> RoleIds) : IRequest<AdminUserDto>;

public class AssignUserRolesCommandHandler : IRequestHandler<AssignUserRolesCommand, AdminUserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AssignUserRolesCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AdminUserDto> Handle(AssignUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Customer)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        // Every requested role must exist.
        var roleIds = request.RoleIds?.Distinct().ToList() ?? new List<Guid>();
        var roles = roleIds.Count == 0
            ? new List<Role>()
            : await _db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        var missing = roleIds.Except(roles.Select(r => r.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException("Role", missing[0]);

        // Reconcile the join rows to exactly the requested set: drop the ones no longer wanted and add
        // the new ones. (UserRole is an explicit join entity, so a blind clear-then-re-add would trip the
        // change-tracker on any role that is being kept.)
        var desired = roles.Select(r => r.Id).ToHashSet();
        foreach (var existing in user.UserRoles.Where(ur => !desired.Contains(ur.RoleId)).ToList())
            user.UserRoles.Remove(existing);

        var assigned = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        foreach (var role in roles.Where(r => !assigned.Contains(r.Id)))
            user.UserRoles.Add(new UserRole { Role = role, AssignedOnUtc = _clock.UtcNow });

        await _db.SaveChangesAsync(cancellationToken);
        return AdminUserDto.From(user);
    }
}
