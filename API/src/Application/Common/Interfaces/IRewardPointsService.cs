namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>Resolved loyalty configuration with defaults already applied (WO-27).</summary>
/// <param name="Enabled">Whether the loyalty points program is active.</param>
/// <param name="EarnRate">Points earned per 1 currency unit spent.</param>
/// <param name="RedeemRate">Points required per 1 currency unit of checkout discount.</param>
public record LoyaltyConfig(bool Enabled, decimal EarnRate, decimal RedeemRate);

/// <summary>
/// Setting keys and in-code defaults for the loyalty points system (WO-27). Defaults live here rather than
/// in the platform-settings seeder, so an install with no <c>loyalty.*</c> rows behaves as "disabled, 1
/// point per unit spent, 100 points per unit redeemed" until an admin saves the config.
/// </summary>
public static class LoyaltySettings
{
    public const string EnabledKey = "loyalty.enabled";
    public const string EarnRateKey = "loyalty.earn-rate";
    public const string RedeemRateKey = "loyalty.redeem-rate";

    public const bool DefaultEnabled = false;
    public const decimal DefaultEarnRate = 1m;
    public const decimal DefaultRedeemRate = 100m;
}

/// <summary>
/// The loyalty-points ledger (WO-27): read a customer's balance, earn points on placed orders, redeem
/// points as a checkout discount, and claw points back on refund. A balance is maintained on
/// <see cref="Domain.Entities.CustomerPointsBalance"/> and every mutation appends a
/// <see cref="Domain.Entities.PointsTransaction"/>. Every mutating call is a no-op when the program is
/// disabled, and the balance is never driven negative.
/// </summary>
public interface IRewardPointsService
{
    /// <summary>Whether the loyalty program is currently enabled.</summary>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>The current loyalty configuration (enabled flag + earn/redeem rates), defaults applied.</summary>
    Task<LoyaltyConfig> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>The customer's current spendable points balance (0 when they have no ledger yet).</summary>
    Task<int> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Credits the points earned by a placed + paid order: floor(<paramref name="orderTotal"/> × earn rate).
    /// Upserts the balance and appends an <c>Earned</c> ledger entry. No-op when disabled or when the amount
    /// earns no whole point.
    /// </summary>
    Task CreditForOrderAsync(Guid customerId, decimal orderTotal, Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>The currency-value discount that <paramref name="points"/> buys at the given redeem rate
    /// (points ÷ rate), floored to whole cents so it never over-credits.</summary>
    decimal ToDiscountValue(int points, decimal redeemRate);

    /// <summary>
    /// Redeems <paramref name="points"/> against an order at placement: validates they do not exceed the
    /// balance, deducts them and appends a <c>Redeemed</c> ledger entry. No-op when disabled.
    /// </summary>
    Task RedeemAsync(Guid customerId, int points, Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claws back the net points still credited to the customer from the given order's earn entries (earned
    /// minus any prior reversal), appending a <c>RefundReversal</c> entry. Clamped so it never drives the
    /// balance negative, and idempotent (a second call finds nothing left to reverse). No-op when disabled.
    /// </summary>
    Task DeductForRefundAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken = default);
}
