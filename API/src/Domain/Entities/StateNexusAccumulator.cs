using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Running economic-nexus totals for a US state over a measurement period (REQ-TAX-004). Gross sales and
/// transaction counts accumulate from completed orders shipped to the state; when either crosses the
/// warning fraction of its statutory threshold an admin alert is raised so registration can be considered.
/// One row per (state, period).
/// </summary>
public class StateNexusAccumulator : AuditableEntity
{
    /// <summary>Two-letter US state code (destination), e.g. "CA".</summary>
    public string StateCode { get; set; } = string.Empty;

    /// <summary>Start of the measurement window (typically the calendar year).</summary>
    public DateTime PeriodStartUtc { get; set; }

    public decimal GrossSales { get; set; }
    public int TransactionCount { get; set; }

    /// <summary>Statutory revenue threshold for the state (default 100,000).</summary>
    public decimal ThresholdAmount { get; set; }

    /// <summary>Statutory transaction-count threshold, when the state uses one (default 200).</summary>
    public int? ThresholdTransactions { get; set; }

    /// <summary>Fraction of a threshold at which an approaching-nexus alert fires (e.g. 0.80 = 80%).</summary>
    public decimal WarningPercent { get; set; } = 0.80m;

    /// <summary>When an approaching-threshold alert was last raised (null = never), for dedupe.</summary>
    public DateTime? LastAlertedAtUtc { get; set; }
}
