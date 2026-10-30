using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Commerce;

/// <summary>Admin view of the tenant's commerce-mode configuration (REQ-INQ-001).</summary>
public class CommerceModeDto
{
    public string Mode { get; set; } = CommerceMode.Standard.ToString();
    public bool ShowPrices { get; set; } = true;
    public bool CollectAddress { get; set; } = true;
    public string InquiryButtonLabel { get; set; } = "Request a Quote";
    public Guid? DefaultStoreId { get; set; }
    public string? NotifyEmails { get; set; }
    public string? SubmitNote { get; set; }

    public static CommerceModeDto From(CommerceModeSettings s) => new()
    {
        Mode = s.Mode.ToString(),
        ShowPrices = s.ShowPrices,
        CollectAddress = s.CollectAddress,
        InquiryButtonLabel = s.InquiryButtonLabel,
        DefaultStoreId = s.DefaultStoreId,
        NotifyEmails = s.NotifyEmails,
        SubmitNote = s.SubmitNote,
    };
}

/// <summary>
/// Public commerce config for the storefront: which mode the shop runs in and the copy/behaviour the
/// inquiry flow needs. Carries nothing sensitive — the notification recipients stay admin-only.
/// </summary>
public class PublicCommerceConfigDto
{
    public string Mode { get; set; } = CommerceMode.Standard.ToString();

    /// <summary>True when the storefront must check out as an inquiry rather than take payment.</summary>
    public bool IsInquiryOnly { get; set; }

    public bool ShowPrices { get; set; } = true;
    public bool CollectAddress { get; set; } = true;
    public string InquiryButtonLabel { get; set; } = "Request a Quote";
    public string? SubmitNote { get; set; }

    public static PublicCommerceConfigDto From(CommerceModeSettings s) => new()
    {
        Mode = s.Mode.ToString(),
        IsInquiryOnly = s.IsInquiryOnly,
        // Prices always show in Standard mode; the toggle only governs the inquiry catalogue.
        ShowPrices = !s.IsInquiryOnly || s.ShowPrices,
        CollectAddress = s.CollectAddress,
        InquiryButtonLabel = s.InquiryButtonLabel,
        SubmitNote = s.SubmitNote,
    };
}
