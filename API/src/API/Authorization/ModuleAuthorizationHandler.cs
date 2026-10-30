using Microsoft.AspNetCore.Authorization;
using HKPartsEcommerce.API.Authentication;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Authorization;

/// <summary>
/// Grants module access when the caller is a SuperAdmin/TenantAdmin (full bypass) or their JWT
/// <c>accessibleModules</c> claim contains the required module.
/// </summary>
public class ModuleAuthorizationHandler : AuthorizationHandler<ModuleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ModuleRequirement requirement)
    {
        var user = context.User;

        if (user.IsInRole(nameof(RoleType.SuperAdmin)) || user.IsInRole(nameof(RoleType.TenantAdmin)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var accessible = user.FindAll("accessibleModules").Select(c => c.Value);
        if (accessible.Contains(requirement.Module))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Machine-to-machine (API key) callers carry no role — access is governed by their scopes alone.
        var scopes = user.FindAll(ApiKeyDefaults.ScopeClaim).Select(c => c.Value);
        if (scopes.Contains(requirement.Module))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
