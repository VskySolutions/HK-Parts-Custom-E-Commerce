using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.AdminUsers;

/// <summary>Creates a new user identity (+ profile) and assigns the given roles.</summary>
public record CreateUserCommand(
    string Email,
    string? Username,
    string Password,
    string FirstName,
    string LastName,
    List<Guid> RoleIds) : IRequest<AdminUserDto>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).Password();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).MaximumLength(200);
    }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, AdminUserDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTimeProvider _clock;

    public CreateUserCommandHandler(IApplicationDbContext db, IPasswordHasher hasher, IDateTimeProvider clock)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<AdminUserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException($"A user with email '{email}' already exists.");

        var username = string.IsNullOrWhiteSpace(request.Username)
            ? email.Split('@')[0]
            : request.Username.Trim();
        if (await _db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            throw new ConflictException($"Username '{username}' is already taken.");

        // Every requested role must exist.
        var roleIds = request.RoleIds?.Distinct().ToList() ?? new List<Guid>();
        var roles = roleIds.Count == 0
            ? new List<Role>()
            : await _db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        var missing = roleIds.Except(roles.Select(r => r.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException("Role", missing[0]);

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            EmailVerified = false,
            IsActive = true,
            Customer = new Customer { FirstName = request.FirstName, LastName = request.LastName },
        };

        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { Role = role, AssignedOnUtc = _clock.UtcNow });

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return AdminUserDto.From(user);
    }
}
