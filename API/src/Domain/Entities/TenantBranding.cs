using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Deployment branding consumed by the Client App at startup and applied as CSS custom properties
/// before first render (Tenant Management blueprint). Effectively a singleton per deployment.
/// </summary>
public class TenantBranding : AuditableEntity
{
    public string BrandName { get; set; } = string.Empty;
    public string? Domain { get; set; }

    /// <summary>Central Media assets for the logo/favicon (preferred). Uploads set these going forward.</summary>
    public Guid? LogoMediaId { get; set; }
    public Media? LogoMedia { get; set; }
    public Guid? FaviconMediaId { get; set; }
    public Media? FaviconMedia { get; set; }

    /// <summary>Legacy logo/favicon URLs, retained as read fallbacks for pre-migration records.</summary>
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }

    // ---- Storefront HTML-tag palette (applied to the public storefront theme by useStorefront) ----
    /// <summary>Storefront page (body) background colour.</summary>
    public string? BodyBackgroundColor { get; set; }
    /// <summary>Default storefront body/text colour (inherited by paragraphs, spans, list items…).</summary>
    public string? TextColor { get; set; }
    /// <summary>General heading colour; H1–H6 fall back to this when their own colour is unset.</summary>
    public string? HeadingColor { get; set; }
    public string? Heading1Color { get; set; }
    public string? Heading2Color { get; set; }
    public string? Heading3Color { get; set; }
    public string? Heading4Color { get; set; }
    public string? Heading5Color { get; set; }
    public string? Heading6Color { get; set; }
    public string? ParagraphColor { get; set; }
    public string? SpanColor { get; set; }
    public string? LinkColor { get; set; }

    public string? FontFamily { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? SocialLinksJson { get; set; }
    public string? LayoutOptionsJson { get; set; }
    public string? DefaultLanguage { get; set; }

    /// <summary>
    /// IANA timezone id (e.g. "America/New_York") for displaying UTC timestamps across the whole
    /// application — the tenant-wide default for the admin app and the storefront (a signed-in
    /// customer may override it via <see cref="Customer.PreferredTimeZone"/>). Stored data stays UTC;
    /// this only affects display.
    /// </summary>
    public string DisplayTimeZone { get; set; } = "UTC";
}
