using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Audit;

/// <summary>
/// Creation/modification actor + timestamps for a single record, powering the standard
/// "Created by / Updated by" footer shown at the bottom of every admin detail page.
/// </summary>
public class RecordAuditDto
{
    public Guid? CreatedById { get; set; }
    /// <summary>Resolved username (or email) of the creating actor; null for system/seed data.</summary>
    public string? CreatedBy { get; set; }
    public DateTime? CreatedOnUtc { get; set; }

    public Guid? UpdatedById { get; set; }
    /// <summary>Resolved username (or email) of the last actor to modify the record.</summary>
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}

/// <summary>
/// Maps a stable, route-friendly entity key to its CLR type and owning admin module. The key is what a
/// detail page passes to the audit endpoint; the module gates access so a caller only sees audit metadata
/// for modules they can already reach. Only <see cref="Domain.Common.AuditableEntity"/> types belong here.
/// </summary>
public static class RecordAuditRegistry
{
    private static readonly IReadOnlyDictionary<string, (Type Type, string Module)> Map =
        new Dictionary<string, (Type, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["product"] = (typeof(Product), Modules.Catalog),
            ["category"] = (typeof(Category), Modules.Catalog),
            ["manufacturer"] = (typeof(Manufacturer), Modules.Catalog),
            ["product-attribute"] = (typeof(ProductAttribute), Modules.Catalog),
            ["specification-attribute"] = (typeof(SpecificationAttribute), Modules.Catalog),
            ["tax-category"] = (typeof(TaxCategory), Modules.Tax),
            ["discount"] = (typeof(Discount), Modules.Promotions),
            ["coupon"] = (typeof(CouponCode), Modules.Promotions),
            ["store"] = (typeof(Store), Modules.Stores),
            ["user"] = (typeof(User), Modules.Users),
            ["role"] = (typeof(Role), Modules.Roles),
            ["customer"] = (typeof(Customer), Modules.Customers),
            ["customer-group"] = (typeof(CustomerGroup), Modules.Customers),
            ["tax-exemption-request"] = (typeof(TaxExemptionRequest), Modules.Customers),
            ["order"] = (typeof(Order), Modules.Orders),
            ["rma"] = (typeof(Domain.Entities.Rma), Modules.Orders),
            ["currency"] = (typeof(SupportedCurrency), Modules.Currencies),
            ["smtp-account"] = (typeof(SmtpAccount), Modules.SmtpAccounts),
            ["email-template"] = (typeof(EmailTemplate), Modules.EmailTemplates),
            ["shipping-method"] = (typeof(ShippingMethod), Modules.Shipping),
            ["shipping-zone"] = (typeof(ShippingZone), Modules.Shipping),
            ["api-key"] = (typeof(ApiKey), Modules.ApiKeys),
            ["webhook"] = (typeof(WebhookSubscription), Modules.Webhooks),
            ["language"] = (typeof(Language), Modules.Languages),
            ["branding"] = (typeof(TenantBranding), Modules.Branding),
        };

    /// <summary>Resolve a registered entity key to its CLR type and owning module.</summary>
    public static bool TryGet(string? key, out Type type, out string module)
    {
        if (key is not null && Map.TryGetValue(key, out var entry))
        {
            type = entry.Type;
            module = entry.Module;
            return true;
        }
        type = null!;
        module = null!;
        return false;
    }
}
