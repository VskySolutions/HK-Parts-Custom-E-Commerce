using System.Globalization;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Pricing;

/// <summary>
/// The loyalty-points ledger (WO-27). A customer's balance lives on a single
/// <see cref="CustomerPointsBalance"/> row and every change appends an immutable
/// <see cref="PointsTransaction"/> carrying the resulting <see cref="PointsTransaction.BalanceAfter"/>.
/// All mutating operations are inert when the program is disabled, and the balance is never driven below
/// zero. Configuration is read from platform settings with in-code defaults (<see cref="LoyaltySettings"/>),
/// parsed with the invariant culture so a comma-decimal locale can't corrupt the rates.
/// </summary>
public class RewardPointsService : IRewardPointsService
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public RewardPointsService(IApplicationDbContext db, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _settings = settings;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        => (await GetConfigAsync(cancellationToken)).Enabled;

    public async Task<LoyaltyConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var enabledRaw = await _settings.GetValueAsync(LoyaltySettings.EnabledKey, cancellationToken);
        var earnRaw = await _settings.GetValueAsync(LoyaltySettings.EarnRateKey, cancellationToken);
        var redeemRaw = await _settings.GetValueAsync(LoyaltySettings.RedeemRateKey, cancellationToken);

        var enabled = bool.TryParse(enabledRaw, out var e) ? e : LoyaltySettings.DefaultEnabled;

        // Earn rate may be 0 (earning switched off); redeem rate must be > 0 (it is a divisor).
        var earnRate = decimal.TryParse(earnRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var er) && er >= 0m
            ? er
            : LoyaltySettings.DefaultEarnRate;
        var redeemRate = decimal.TryParse(redeemRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var rr) && rr > 0m
            ? rr
            : LoyaltySettings.DefaultRedeemRate;

        return new LoyaltyConfig(enabled, earnRate, redeemRate);
    }

    public async Task<int> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await _db.CustomerPointsBalances.AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .Select(b => (int?)b.Balance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

    public async Task CreditForOrderAsync(Guid customerId, decimal orderTotal, Guid orderId, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(cancellationToken);
        if (!config.Enabled || orderTotal <= 0m)
            return;

        var points = (int)Math.Floor(orderTotal * config.EarnRate);
        if (points <= 0)
            return;

        await AppendAsync(customerId, points, PointsTransactionType.Earned, "Points earned on order.", orderId, cancellationToken);
    }

    public decimal ToDiscountValue(int points, decimal redeemRate)
    {
        if (points <= 0 || redeemRate <= 0m)
            return 0m;

        // Floor to whole cents so the discount can never exceed the points' fair value.
        var raw = points / redeemRate;
        return Math.Truncate(raw * 100m) / 100m;
    }

    public async Task RedeemAsync(Guid customerId, int points, Guid orderId, CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken) || points <= 0)
            return;

        var balance = await GetBalanceAsync(customerId, cancellationToken);
        if (points > balance)
            throw new ConflictException($"Insufficient points: the balance is {balance} but {points} were requested.");

        await AppendAsync(customerId, -points, PointsTransactionType.Redeemed, "Points redeemed on order.", orderId, cancellationToken);
    }

    public async Task DeductForRefundAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
            return;

        // Net points still credited to this customer from THIS order's earn entries: what was earned, less
        // any reversal already booked against the same order (so a repeat call reverses nothing further).
        var netEarned = await _db.PointsTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId
                        && t.OrderId == orderId
                        && (t.Type == PointsTransactionType.Earned || t.Type == PointsTransactionType.RefundReversal))
            .SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;
        if (netEarned <= 0)
            return;

        // Never drive the balance negative — the customer may already have spent some of the earned points.
        var balance = await GetBalanceAsync(customerId, cancellationToken);
        var clawback = Math.Min(netEarned, balance);
        if (clawback <= 0)
            return;

        await AppendAsync(customerId, -clawback, PointsTransactionType.RefundReversal, "Points reversed on refund.", orderId, cancellationToken);
    }

    /// <summary>
    /// Upserts the customer's balance by <paramref name="delta"/> and appends a matching ledger entry,
    /// stamping <see cref="PointsTransaction.BalanceAfter"/> with the running total. Guards the balance
    /// against going negative (a defensive backstop — callers validate before debiting).
    /// </summary>
    private async Task AppendAsync(
        Guid customerId, int delta, PointsTransactionType type, string? reason, Guid? orderId, CancellationToken cancellationToken)
    {
        var balance = await _db.CustomerPointsBalances
            .FirstOrDefaultAsync(b => b.CustomerId == customerId, cancellationToken);
        if (balance is null)
        {
            balance = new CustomerPointsBalance { CustomerId = customerId, Balance = 0 };
            _db.CustomerPointsBalances.Add(balance);
        }

        var newBalance = balance.Balance + delta;
        if (newBalance < 0)
            throw new ConflictException("The customer does not have enough points for this operation.");

        balance.Balance = newBalance;

        _db.PointsTransactions.Add(new PointsTransaction
        {
            CustomerId = customerId,
            Points = delta,
            Type = type,
            Reason = reason,
            OrderId = orderId,
            BalanceAfter = newBalance,
            CreatedOnUtc = _clock.UtcNow,
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
