using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Currencies;

/// <summary>A display currency with its exchange rate relative to the base currency.</summary>
public class CurrencyDto
{
    public Guid Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsBaseCurrency { get; set; }
    public bool IsRateLocked { get; set; }
    public DateTime? LastRateUpdatedOnUtc { get; set; }

    public static CurrencyDto From(SupportedCurrency c) => new()
    {
        Id = c.Id,
        CurrencyCode = c.CurrencyCode,
        Symbol = c.Symbol,
        ExchangeRate = c.ExchangeRate,
        IsEnabled = c.IsEnabled,
        IsBaseCurrency = c.IsBaseCurrency,
        IsRateLocked = c.IsRateLocked,
        LastRateUpdatedOnUtc = c.LastRateUpdatedOnUtc,
    };
}

/// <summary>Configuration for the scheduled refresh of unlocked exchange rates (WO-90).</summary>
public class AutoRefreshConfigDto
{
    public bool Enabled { get; set; }
    public int IntervalHours { get; set; }
    public string? SourceUrl { get; set; }
}
