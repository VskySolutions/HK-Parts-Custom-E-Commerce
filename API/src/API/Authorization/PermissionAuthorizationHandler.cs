using Microsoft.AspNetCore.Authorization;
using HKPartsEcommerce.API.Authentication;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Authorization;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> using the same rule the admin SPA applies
/// (<c>stores/auth.js → hasPermission</c>):
/// <list type="bullet">
/// <item>SuperAdmin / TenantAdmin hold every permission (role-based full access).</item>
/// <item>Otherwise the JWT must carry the permission in a <c>permissions</c>/<c>permission</c> claim.</item>
/// <item>API-key (machine-to-machine) callers carry no role; their access stays governed by their module
/// scopes alone (already enforced by <see cref="RequireModuleAttribute"/>), so they are not further restricted.</item>
/// </list>
/// Custom staff roles that only have module access therefore get read-only access to write-gated endpoints,
/// matching what the UI already allows them.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly string[] PermissionClaimTypes = { "permissions", "permission" };

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;

        if (user.IsInRole(nameof(RoleType.SuperAdmin)) || user.IsInRole(nameof(RoleType.TenantAdmin)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var granted = PermissionClaimTypes
            .SelectMany(t => user.FindAll(t))
            .Any(c => string.Equals(c.Value, requirement.Permission, StringComparison.Ordinal));
        if (granted)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (user.HasClaim(c => c.Type == ApiKeyDefaults.ApiKeyIdClaim))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
