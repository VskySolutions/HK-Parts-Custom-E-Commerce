using Microsoft.AspNetCore.Authorization;

namespace HKPartsEcommerce.API.Authorization;

/// <summary>
/// Restricts an endpoint to callers holding the given permission. Combine with <see cref="RequireModuleAttribute"/>
/// on the controller: both policies must pass. Usage: <c>[RequirePermission(Permissions.UsersWrite)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) => Policy = ModulePolicyProvider.PermissionPrefix + permission;
}
