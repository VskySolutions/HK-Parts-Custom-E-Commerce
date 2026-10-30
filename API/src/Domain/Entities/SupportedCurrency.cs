using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A display currency with an exchange rate relative to the base currency. When
/// <see cref="IsRateLocked"/> is true the rate is fixed and excluded from auto-refresh
/// (Currency configuration, Tenant Management blueprint).
/// </summary>
public class SupportedCurrency : AuditableEntity
{
    public string CurrencyCode { get; set; } = string.Empty; // ISO 4217
    public string Symbol { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1m;
    public bool IsEnabled { get; set; } = true;
    public bool IsBaseCurrency { get; set; }
    public bool IsRateLocked { get; set; }
    public DateTime? LastRateUpdatedOnUtc { get; set; }
}
