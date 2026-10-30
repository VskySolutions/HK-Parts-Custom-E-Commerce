using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A role that can be assigned to users. System roles (SuperAdmin/TenantAdmin) are seeded.</summary>
public class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Modules a custom role may access (empty for SuperAdmin/TenantAdmin, which bypass module checks).
    /// Emitted as the JWT <c>accessibleModules</c> claim so the frontend can filter nav and the API can
    /// enforce module-level access without a per-request DB lookup (Authentication and Authorization blueprint).
    /// </summary>
    public List<string> AccessibleModules { get; set; } = new();

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
