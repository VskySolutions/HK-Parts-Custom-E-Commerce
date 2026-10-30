namespace HKPartsEcommerce.Domain.Enums;

/// <summary>
/// How this tenant sells. <see cref="Standard"/> is the full commerce flow (payment, shipping, tax);
/// <see cref="InquiryOnly"/> turns the storefront into a quote-request catalogue — the buyer submits an
/// inquiry instead of paying, and no gateway, carrier or tax provider is ever called. Stored as the
/// <c>commerce.mode</c> platform setting and resolved through <c>ICommerceModeService</c>.
/// </summary>
public enum CommerceMode
{
    Standard = 0,
    InquiryOnly = 1
}
