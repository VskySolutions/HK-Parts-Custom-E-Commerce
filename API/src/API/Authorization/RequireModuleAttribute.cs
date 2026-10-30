using Microsoft.AspNetCore.Authorization;

namespace HKPartsEcommerce.API.Authorization;

/// <summary>
/// Restricts an endpoint to callers who can access the given module. SuperAdmin/TenantAdmin bypass;
/// other roles must carry the module in their JWT <c>accessibleModules</c> claim.
/// Usage: <c>[RequireModule(Modules.Stores)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireModuleAttribute : AuthorizeAttribute
{
    public RequireModuleAttribute(string module) => Policy = ModulePolicyProvider.Prefix + module;
}
