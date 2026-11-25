using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Singleton configuration for tax calculation (REQ-TAX-002): which provider is active and the
/// flat-rate fallback used when the provider is unavailable or a region is unsupported.
/// </summary>
public class TaxProviderConfiguration : AuditableEntity
{
    public TaxProviderType ActiveProvider { get; set; } = TaxProviderType.FlatRate;

    /// <summary>Flat-rate fallback as a percentage (e.g. 8.5 = 8.5%).</summary>
    public decimal FlatRatePercent { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Cache TTL for identical origin/destination/product tax results (AC-TAX-001.5).</summary>
    public int CacheTtlMinutes { get; set; } = 60;

    /// <summary>
    /// PHP "Tax Shipping Amount": add the shipping charge to the taxable amount for the rate methods (flat rate,
    /// by state, by ZIP). Not applied to Avalara, whose nexus settings decide whether shipping is taxed.
    /// </summary>
    public bool TaxShipping { get; set; } = true;

    // ---- Avalara (PHP Avalara Settings). Account, licence key, URL and company code live on the Avalara credential.

    /// <summary>PHP "Document Committing": commit a SalesInvoice when a paid order is placed, void it on cancel.</summary>
    public bool AvalaraCommitDocuments { get; set; } = true;

    /// <summary>PHP "Logging": record each Avalara request/response in <see cref="TaxLog"/>.</summary>
    public bool AvalaraLoggingEnabled { get; set; } = true;

    /// <summary>PHP "Request Timeout", in seconds.</summary>
    public int AvalaraTimeoutSeconds { get; set; } = 10;

    /// <summary>PHP "Default Shipping Tax Code": the Avalara tax code sent on the shipping line.</summary>
    public string? AvalaraShippingTaxCode { get; set; } = "FR020100";
}
