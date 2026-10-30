namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Refreshes unlocked exchange rates from the configured provider when auto-refresh is enabled.
/// Rate-locked currencies are intentionally skipped (WO-90 locked-rate requirement).
/// </summary>
public interface ICurrencyRateRefresher
{
    /// <summary>Refreshes rates and returns the number of currencies updated (0 when disabled or on failure).</summary>
    Task<int> RefreshAsync(CancellationToken cancellationToken = default);
}
