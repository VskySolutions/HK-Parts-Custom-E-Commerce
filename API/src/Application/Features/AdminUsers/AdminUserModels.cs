using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.AdminUsers;

/// <summary>Admin-facing view of a user identity: its profile name, status and assigned roles.</summary>
public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool EmailVerified { get; set; }
    public IReadOnlyList<RoleRef> Roles { get; set; } = new List<RoleRef>();

    /// <summary>Projects a user into a DTO. Expects <c>.Customer</c> and <c>.UserRoles.Role</c> to be loaded.</summary>
    public static AdminUserDto From(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FullName = u.Customer is null
            ? string.Empty
            : $"{u.Customer.FirstName} {u.Customer.LastName}".Trim(),
        IsActive = u.IsActive,
        EmailVerified = u.EmailVerified,
        Roles = u.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => new RoleRef { Id = ur.Role!.Id, Name = ur.Role!.Name })
            .ToList(),
    };
}

/// <summary>Lightweight reference to a role assigned to a user.</summary>
public class RoleRef
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
