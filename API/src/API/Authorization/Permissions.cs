namespace HKPartsEcommerce.API.Authorization;

/// <summary>
/// Canonical <c>&lt;Resource&gt;.&lt;Action&gt;</c> permission strings enforced server-side with
/// <see cref="RequirePermissionAttribute"/>. They mirror the frontend's frozen catalog
/// (<c>WEB/src/composables/usePermissions.js</c>) so the UI gate and the API gate use the same names.
/// </summary>
public static class Permissions
{
    /// <summary>Write access to user/customer records (the admin UI's <c>canWrite</c> gate on the Customers module).</summary>
    public const string UsersWrite = "Users.Write";

    /// <summary>Blacklist (block) a customer from the Customers list.</summary>
    public const string CustomersBlacklist = "Customers.Blacklist";
}
