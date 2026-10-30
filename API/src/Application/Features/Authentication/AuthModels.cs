using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Authentication;

/// <summary>Authentication result returned by login and refresh.</summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public AuthUserDto User { get; set; } = new();
}

public class AuthUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> AccessibleModules { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Resolves the effective role names and accessible-module list for a user from their role assignments.
/// SuperAdmin/TenantAdmin bypass module checks, so their accessible-module list is empty.
/// </summary>
public static class AccessScope
{
    private static readonly string[] FullAccessRoles = { "SuperAdmin", "TenantAdmin" };

    public static (List<string> Roles, List<string> AccessibleModules) From(IEnumerable<UserRole> userRoles)
    {
        var roleEntities = userRoles.Where(ur => ur.Role is not null).Select(ur => ur.Role!).ToList();
        var roles = roleEntities.Select(r => r.Name).Distinct().ToList();

        var fullAccess = roles.Any(r => FullAccessRoles.Contains(r));
        var modules = fullAccess
            ? new List<string>()
            : roleEntities.SelectMany(r => r.AccessibleModules).Distinct().ToList();

        return (roles, modules);
    }
}

/// <summary>Builds an <see cref="AuthResponse"/> from a user, its roles/modules, and freshly issued tokens.</summary>
public static class AuthResponseFactory
{
    public static AuthResponse Create(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> accessibleModules,
        string accessToken,
        DateTime expiresAtUtc,
        string refreshToken) => new()
    {
        AccessToken = accessToken,
        ExpiresAtUtc = expiresAtUtc,
        RefreshToken = refreshToken,
        User = new AuthUserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.Customer is null
                ? string.Empty
                : $"{user.Customer.FirstName} {user.Customer.LastName}".Trim(),
            Roles = roles,
            AccessibleModules = accessibleModules,
        },
    };
}
