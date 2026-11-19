namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Who checks the checkout ship-to address (docs/shipstation-verification-requirements.md §4.3).</summary>
public enum AddressVerificationProvider
{
    None = 0,
    Avalara = 1,
    Usps = 2,
}

/// <summary>
/// Outcome of verifying one address (§6.1 <c>status</c>). <see cref="NotChecked"/> means verification did not
/// run for it (disabled, country not in the list, pickup, an FFL dealer from the directory).
/// </summary>
public enum AddressVerificationStatus
{
    NotChecked = 0,
    /// <summary>The provider confirmed the address as entered.</summary>
    Valid = 1,
    /// <summary>The provider confirmed it with corrections (ZIP+4, abbreviations, city spelling) (AV-13).</summary>
    Suggested = 2,
    /// <summary>The provider could not find the address (AV-10).</summary>
    Invalid = 3,
    /// <summary>The provider was down, timed out or refused the credentials (AV-14).</summary>
    Unavailable = 4,
    /// <summary>The P.O. box rule refused the address (AV-30).</summary>
    PoBox = 5,
}

/// <summary>What checkout does when the provider cannot answer (§5.2 "When provider is down").</summary>
public enum AddressUnavailablePolicy
{
    /// <summary>Let the order continue and flag it as not verified (default, AV-14).</summary>
    AllowAndFlag = 0,
    Block = 1,
}
