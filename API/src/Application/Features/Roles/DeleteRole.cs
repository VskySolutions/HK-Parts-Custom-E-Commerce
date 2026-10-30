using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Roles;

/// <summary>Deletes a custom role (idempotent); assigned UserRole rows cascade. System roles cannot be deleted.</summary>
public record DeleteRoleCommand(Guid Id) : IRequest;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteRoleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (role is null)
            return;

        if (role.IsSystemRole)
            throw new ConflictException("System roles cannot be deleted.");

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
