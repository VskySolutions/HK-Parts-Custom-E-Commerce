using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>Admin view of the singleton tax provider configuration (REQ-TAX-002).</summary>
public class TaxConfigurationDto
{
    public Guid Id { get; set; }
    public TaxProviderType ActiveProvider { get; set; }
    public decimal FlatRatePercent { get; set; }
    public bool IsEnabled { get; set; }
    public int CacheTtlMinutes { get; set; }

    public static TaxConfigurationDto From(TaxProviderConfiguration c) => new()
    {
        Id = c.Id,
        ActiveProvider = c.ActiveProvider,
        FlatRatePercent = c.FlatRatePercent,
        IsEnabled = c.IsEnabled,
        CacheTtlMinutes = c.CacheTtlMinutes,
    };
}
