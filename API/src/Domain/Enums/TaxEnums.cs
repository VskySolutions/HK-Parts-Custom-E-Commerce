namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Active tax calculation provider (REQ-TAX-002).</summary>
public enum TaxProviderType
{
    FlatRate = 0,
    TaxJar = 1,
    StripeTax = 2,

    // PHP parity (legacy ecommcore tax_method): none / state / zip / avalara.
    /// <summary>No tax charged ("No Tax Charged"; on a customer group: "Tax Exempt").</summary>
    None = 3,
    /// <summary>Rate looked up by the ship-to state (<c>TaxStateRates</c>).</summary>
    ByState = 4,
    /// <summary>Rate looked up by the first 5 characters of the ship-to ZIP/postal code (<c>TaxZipRates</c>).</summary>
    ByZip = 5,
    /// <summary>Avalara AvaTax v2: estimate at checkout, commit on placement, void on cancel.</summary>
    Avalara = 6
}
