using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Roles;

/// <summary>Creates a custom (non-system) role granting access to the given admin modules.</summary>
public record CreateRoleCommand(string Name, string? Description, List<string> AccessibleModules)
    : IRequest<RoleDto>;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleForEach(x => x.AccessibleModules)
            .Must(Modules.IsValid)
            .WithMessage("Unknown module: '{PropertyValue}'.");
    }
}

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleDto>
{
    private readonly IApplicationDbContext _db;

    public CreateRoleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var normalizedName = name.ToUpperInvariant();

        var exists = await _db.Roles
            .AnyAsync(r => r.NormalizedName == normalizedName, cancellationToken);
        if (exists)
            throw new ConflictException($"A role named '{name}' already exists.");

        var role = new Role
        {
            Name = name,
            NormalizedName = normalizedName,
            Description = request.Description,
            IsSystemRole = false,
            AccessibleModules = request.AccessibleModules.Distinct().ToList(),
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);
        return RoleDto.From(role);
    }
}
