using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HKPartsEcommerce.API.Authorization;

/// <summary>
/// Dynamically materializes "module:&lt;name&gt;" authorization policies (each carrying a
/// <see cref="ModuleRequirement"/>); all other policy names fall through to the default provider so
/// the role-tier policies still resolve.
/// </summary>
public class ModulePolicyProvider : IAuthorizationPolicyProvider
{
    public const string Prefix = "module:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public ModulePolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var module = policyName[Prefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ModuleRequirement(module))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
