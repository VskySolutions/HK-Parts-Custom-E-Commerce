using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// PHP parity (ecomm_tax_state): the sales-tax rate charged when the tax method is "By State". One row per
/// state; a blank <see cref="RatePercent"/> (or 0) charges no tax there.
/// </summary>
public class TaxStateRate : AuditableEntity
{
    public string CountryCode { get; set; } = "US";

    /// <summary>2-letter state code (e.g. "FL").</summary>
    public string StateCode { get; set; } = string.Empty;

    public string StateName { get; set; } = string.Empty;

    /// <summary>Percentage (6.5 = 6.5%). Null = no rate set.</summary>
    public decimal? RatePercent { get; set; }
}

/// <summary>
/// PHP parity (ecomm_tax_zip): the sales-tax rate charged when the tax method is "By ZIP/Postal Code". Matched on
/// the first 5 characters of the ship-to postal code.
/// </summary>
public class TaxZipRate : AuditableEntity
{
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Percentage (6.5 = 6.5%).</summary>
    public decimal RatePercent { get; set; }
}

/// <summary>
/// PHP parity (ecomm_log with log_type avalara): one tax-provider request and its response, recorded when the
/// provider's logging is on. Shown on the admin Tax, Avalara log tab.
/// </summary>
public class TaxLog : BaseEntity
{
    /// <summary>The provider, e.g. "Avalara".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Get Tax / Commit Order / Void Tax / Test Connection.</summary>
    public string Action { get; set; } = string.Empty;

    public bool Success { get; set; }
    public string? OrderNumber { get; set; }
    public string? Url { get; set; }
    public int? HttpStatus { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}
