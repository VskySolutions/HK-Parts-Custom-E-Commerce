using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Roles;

/// <summary>Role view exposing which admin modules the role grants access to.</summary>
public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public IReadOnlyList<string> AccessibleModules { get; set; } = new List<string>();

    public static RoleDto From(Role r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        IsSystemRole = r.IsSystemRole,
        AccessibleModules = r.AccessibleModules.ToList(),
    };
}
