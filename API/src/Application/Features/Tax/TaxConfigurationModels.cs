using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>Admin view of the singleton tax configuration (REQ-TAX-002 + PHP Tax / Avalara settings).</summary>
public class TaxConfigurationDto
{
    public Guid Id { get; set; }
    public TaxProviderType ActiveProvider { get; set; }
    public decimal FlatRatePercent { get; set; }
    public bool IsEnabled { get; set; }
    public int CacheTtlMinutes { get; set; }

    /// <summary>PHP "Tax Shipping Amount".</summary>
    public bool TaxShipping { get; set; }

    public bool AvalaraCommitDocuments { get; set; }
    public bool AvalaraLoggingEnabled { get; set; }
    public int AvalaraTimeoutSeconds { get; set; }
    public string? AvalaraShippingTaxCode { get; set; }

    /// <summary>Whether an active Avalara credential (URL, account id, licence key) is on file. Read-only.</summary>
    public bool AvalaraConfigured { get; set; }

    /// <summary>The company code on the active Avalara credential (blank = DEFAULT). Read-only.</summary>
    public string? AvalaraCompanyCode { get; set; }

    public static TaxConfigurationDto From(TaxProviderConfiguration c) => new()
    {
        Id = c.Id,
        ActiveProvider = c.ActiveProvider,
        FlatRatePercent = c.FlatRatePercent,
        IsEnabled = c.IsEnabled,
        CacheTtlMinutes = c.CacheTtlMinutes,
        TaxShipping = c.TaxShipping,
        AvalaraCommitDocuments = c.AvalaraCommitDocuments,
        AvalaraLoggingEnabled = c.AvalaraLoggingEnabled,
        AvalaraTimeoutSeconds = c.AvalaraTimeoutSeconds,
        AvalaraShippingTaxCode = c.AvalaraShippingTaxCode,
    };
}
