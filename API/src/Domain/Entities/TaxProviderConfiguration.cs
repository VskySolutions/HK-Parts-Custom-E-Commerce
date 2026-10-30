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
}
