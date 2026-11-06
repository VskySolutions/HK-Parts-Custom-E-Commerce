using Microsoft.AspNetCore.Authorization;

namespace HKPartsEcommerce.API.Authorization;

/// <summary>Authorization requirement satisfied when the caller holds a given permission (e.g. <c>Users.Write</c>).</summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}
