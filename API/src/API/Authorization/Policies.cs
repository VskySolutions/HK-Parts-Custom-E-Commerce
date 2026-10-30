namespace HKPartsEcommerce.API.Authorization;

/// <summary>
/// Authorization policy names for the two-tier role model. Each policy admits its own role and all
/// higher roles (SuperAdmin ⊇ TenantAdmin).
/// </summary>
public static class Policies
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
}
