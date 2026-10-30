using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Roles;

/// <summary>Updates a custom role's name, description and accessible modules. System roles are immutable.</summary>
public record UpdateRoleCommand(Guid Id, string Name, string? Description, List<string> AccessibleModules)
    : IRequest<RoleDto>;

public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleForEach(x => x.AccessibleModules)
            .Must(Modules.IsValid)
            .WithMessage("Unknown module: '{PropertyValue}'.");
    }
}

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateRoleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Role", request.Id);

        if (role.IsSystemRole)
            throw new ConflictException("System roles cannot be modified.");

        var name = request.Name.Trim();
        var normalizedName = name.ToUpperInvariant();

        var taken = await _db.Roles
            .AnyAsync(r => r.Id != request.Id && r.NormalizedName == normalizedName, cancellationToken);
        if (taken)
            throw new ConflictException($"A role named '{name}' already exists.");

        role.Name = name;
        role.NormalizedName = normalizedName;
        role.Description = request.Description;
        role.AccessibleModules = request.AccessibleModules.Distinct().ToList();

        await _db.SaveChangesAsync(cancellationToken);
        return RoleDto.From(role);
    }
}
