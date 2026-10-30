using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// The tenant's resolved commerce configuration (the <c>commerce.*</c> platform settings, REQ-INQ-001).
/// In <see cref="CommerceMode.InquiryOnly"/> the storefront is a quote-request catalogue: checkout takes
/// no payment and never calls a gateway, carrier or tax provider.
/// </summary>
public record CommerceModeSettings(
    CommerceMode Mode,
    bool ShowPrices,
    bool CollectAddress,
    string InquiryButtonLabel,
    Guid? DefaultStoreId,
    string? NotifyEmails,
    string? SubmitNote)
{
    /// <summary>True when the whole tenant sells by inquiry (as opposed to per-product inquiry items).</summary>
    public bool IsInquiryOnly => Mode == CommerceMode.InquiryOnly;

    /// <summary>The settings a tenant gets before anything is configured: the full commerce flow.</summary>
    public static CommerceModeSettings Default => new(
        CommerceMode.Standard, ShowPrices: true, CollectAddress: true,
        InquiryButtonLabel: "Request a Quote", DefaultStoreId: null, NotifyEmails: null, SubmitNote: null);
}

/// <summary>Well-known keys of the <c>commerce.*</c> platform settings.</summary>
public static class CommerceSettingKeys
{
    public const string Mode = "commerce.mode";
    public const string ShowPrices = "commerce.inquiry.show-prices";
    public const string CollectAddress = "commerce.inquiry.collect-address";
    public const string ButtonLabel = "commerce.inquiry.button-label";
    public const string DefaultStoreId = "commerce.inquiry.default-store-id";
    public const string NotifyEmails = "commerce.inquiry.notify-emails";
    public const string SubmitNote = "commerce.inquiry.submit-note";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Mode, ShowPrices, CollectAddress, ButtonLabel, DefaultStoreId, NotifyEmails, SubmitNote,
    };
}
