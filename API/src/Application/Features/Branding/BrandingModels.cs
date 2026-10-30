using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Branding;

/// <summary>Deployment branding consumed by the Client App at startup and applied before first render.</summary>
public class BrandingDto
{
    public string BrandName { get; set; } = string.Empty;
    public string? Domain { get; set; }

    /// <summary>Central Media asset ids for the logo/favicon (preferred going forward; null for un-migrated records).</summary>
    public Guid? LogoMediaId { get; set; }
    public Guid? FaviconMediaId { get; set; }

    /// <summary>Resolved logo/favicon URLs: the Media asset's URL when present, else the legacy fallback column.</summary>
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }

    /// <summary>Storefront HTML-tag palette — page background, body text, headings (general + H1–H6),
    /// paragraph, span and link colours, applied onto the storefront <c>--sf-*</c> tokens.</summary>
    public string? BodyBackgroundColor { get; set; }
    public string? TextColor { get; set; }
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

    /// <summary>Tenant-wide IANA display timezone for UTC timestamps (whole-app default).</summary>
    public string DisplayTimeZone { get; set; } = "UTC";

    public static BrandingDto From(TenantBranding b) => new()
    {
        BrandName = b.BrandName,
        Domain = b.Domain,
        LogoMediaId = b.LogoMediaId,
        FaviconMediaId = b.FaviconMediaId,
        LogoUrl = b.LogoMedia?.Url ?? b.LogoUrl,
        FaviconUrl = b.FaviconMedia?.Url ?? b.FaviconUrl,
        PrimaryColor = b.PrimaryColor,
        SecondaryColor = b.SecondaryColor,
        AccentColor = b.AccentColor,
        BodyBackgroundColor = b.BodyBackgroundColor,
        TextColor = b.TextColor,
        HeadingColor = b.HeadingColor,
        Heading1Color = b.Heading1Color,
        Heading2Color = b.Heading2Color,
        Heading3Color = b.Heading3Color,
        Heading4Color = b.Heading4Color,
        Heading5Color = b.Heading5Color,
        Heading6Color = b.Heading6Color,
        ParagraphColor = b.ParagraphColor,
        SpanColor = b.SpanColor,
        LinkColor = b.LinkColor,
        FontFamily = b.FontFamily,
        SupportEmail = b.SupportEmail,
        SupportPhone = b.SupportPhone,
        SocialLinksJson = b.SocialLinksJson,
        LayoutOptionsJson = b.LayoutOptionsJson,
        DefaultLanguage = b.DefaultLanguage,
        DisplayTimeZone = string.IsNullOrWhiteSpace(b.DisplayTimeZone) ? "UTC" : b.DisplayTimeZone,
    };
}
