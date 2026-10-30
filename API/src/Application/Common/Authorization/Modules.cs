namespace HKPartsEcommerce.Application.Common.Authorization;

/// <summary>An admin module a role can be granted access to.</summary>
public record ModuleInfo(string Key, string DisplayName);

/// <summary>
/// Canonical set of admin modules. A custom role grants access to a subset via
/// <see cref="Domain.Entities.Role.AccessibleModules"/>; SuperAdmin/TenantAdmin bypass all module checks.
/// </summary>
public static class Modules
{
    public const string Stores = "Stores";
    public const string Currencies = "Currencies";
    public const string EmailTemplates = "EmailTemplates";
    public const string Branding = "Branding";
    public const string Settings = "Settings";
    public const string Credentials = "Credentials";
    public const string SmtpAccounts = "SmtpAccounts";
    public const string Storage = "Storage";
    public const string BackgroundTasks = "BackgroundTasks";
    public const string Alerts = "Alerts";
    public const string Users = "Users";
    public const string Roles = "Roles";
    public const string ApiKeys = "ApiKeys";
    public const string Catalog = "Catalog";
    public const string Inventory = "Inventory";
    public const string Customers = "Customers";
    public const string Promotions = "Promotions";
    public const string Shipping = "Shipping";
    public const string Payments = "Payments";
    public const string Tax = "Tax";
    public const string Orders = "Orders";
    public const string Webhooks = "Webhooks";
    public const string Languages = "Languages";
    public const string EmailLog = "EmailLog";
    // Phase 5
    public const string Cms = "Cms";
    public const string Dashboard = "Dashboard";
    public const string Logs = "Logs";
    public const string AuditTrail = "AuditTrail";
    // Inquiries (REQ-INQ-001)
    public const string Inquiries = "Inquiries";

    public static readonly IReadOnlyList<ModuleInfo> All = new List<ModuleInfo>
    {
        new(Stores, "Store Management"),
        new(Currencies, "Currency Configuration"),
        new(EmailTemplates, "Email Templates"),
        new(Branding, "Tenant Branding"),
        new(Settings, "Platform Settings"),
        new(Credentials, "Integration Credentials"),
        new(SmtpAccounts, "SMTP Accounts"),
        new(Storage, "File Storage"),
        new(BackgroundTasks, "Background Tasks"),
        new(Alerts, "Admin Alerts"),
        new(Users, "User Management"),
        new(Roles, "Role Management"),
        new(ApiKeys, "API Key Management"),
        new(Catalog, "Catalog Management"),
        new(Inventory, "Inventory Management"),
        new(Customers, "Customer Management"),
        new(Promotions, "Discounts & Coupons"),
        new(Shipping, "Shipping Configuration"),
        new(Payments, "Payment Management"),
        new(Tax, "Tax Configuration"),
        new(Orders, "Order Management"),
        new(Webhooks, "Webhooks"),
        new(Languages, "Languages & Translations"),
        new(EmailLog, "Email Log"),
        new(Cms, "CMS / Content"),
        new(Dashboard, "Dashboard & Reports"),
        new(Logs, "Application Logs"),
        new(AuditTrail, "Audit Trail"),
        new(Inquiries, "Inquiries"),
    };

    private static readonly HashSet<string> Keys = new(All.Select(m => m.Key), StringComparer.Ordinal);

    public static bool IsValid(string module) => Keys.Contains(module);
}
