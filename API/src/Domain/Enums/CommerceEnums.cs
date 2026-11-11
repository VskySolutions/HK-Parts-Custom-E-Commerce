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

/// <summary>Whether a blacklist entry is enforced (PY-10).</summary>
public enum CustomerBlacklistStatus
{
    Blocked = 0,
    Inactive = 1
}

/// <summary>How a blacklist entry matches a buyer at checkout (PY-15).</summary>
public enum BlacklistMatchMode
{
    /// <summary>Email only: never blocks a different person who shares a name.</summary>
    EmailOnly = 0,
    /// <summary>Both the billing first + last name and the email must match.</summary>
    NameAndEmail = 1,
    /// <summary>Either matches (the legacy behaviour).</summary>
    NameOrEmail = 2
}
