using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Checkout;

namespace HKPartsEcommerce.Infrastructure.Checkout;

/// <summary>
/// SQL Server implementation of <see cref="ICheckoutLockService"/>. Each product is acquired with one
/// <c>MERGE … WITH (HOLDLOCK)</c>, so of two carts racing for the same product exactly one wins (CK-20),
/// without an explicit transaction around the checkout.
/// </summary>
public class CheckoutLockService : ICheckoutLockService
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CheckoutLockService> _logger;

    public CheckoutLockService(IApplicationDbContext db, ISettingsService settings, IDateTimeProvider clock, ILogger<CheckoutLockService> logger)
    {
        _db = db;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DateTime?> AcquireAsync(Guid cartId, IReadOnlyCollection<CheckoutLockItem> items, CancellationToken ct)
    {
        var settings = await CheckoutSettingsStore.LoadAsync(_settings, ct);
        if (!settings.LockEnabled || items.Count == 0)
            return null;

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();

        // Available stock per product across every store and variant. A product with no inventory rows is not
        // stock-tracked, so it never locks.
        var stock = await _db.InventoryLevels.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId))
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Available = g.Sum(l => l.StockQuantity - l.ReservedQuantity) })
            .ToListAsync(ct);
        var lowStock = stock.Where(s => s.Available < settings.LockQtyThreshold).Select(s => s.ProductId).ToHashSet();
        if (lowStock.Count == 0)
            return null;

        var now = _clock.UtcNow;
        var expires = now.AddSeconds(settings.LockSeconds);
        DateTime? soonest = null;

        foreach (var item in items.Where(i => lowStock.Contains(i.ProductId)).DistinctBy(i => i.ProductId))
        {
            // Take the row when it is free, expired or already ours. Our own valid lock keeps its deadline.
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
MERGE [CheckoutLocks] WITH (HOLDLOCK) AS t
USING (SELECT {item.ProductId} AS ProductId) AS s ON t.[ProductId] = s.ProductId
WHEN MATCHED AND (t.[OwnerCartId] = {cartId} OR t.[ExpiresAtUtc] <= {now}) THEN UPDATE SET
    t.[LockedAtUtc]  = CASE WHEN t.[OwnerCartId] = {cartId} AND t.[ExpiresAtUtc] > {now} THEN t.[LockedAtUtc]  ELSE {now} END,
    t.[ExpiresAtUtc] = CASE WHEN t.[OwnerCartId] = {cartId} AND t.[ExpiresAtUtc] > {now} THEN t.[ExpiresAtUtc] ELSE {expires} END,
    t.[OwnerCartId]  = {cartId}
WHEN NOT MATCHED THEN
    INSERT ([Id], [ProductId], [OwnerCartId], [LockedAtUtc], [ExpiresAtUtc])
    VALUES (NEWID(), {item.ProductId}, {cartId}, {now}, {expires});", ct);

            var row = await _db.CheckoutLocks.AsNoTracking()
                .Where(l => l.ProductId == item.ProductId)
                .Select(l => new { l.OwnerCartId, l.ExpiresAtUtc })
                .FirstOrDefaultAsync(ct);

            if (row is null || row.OwnerCartId != cartId)
                throw CheckoutErrorCodes.Conflict(
                    CheckoutErrorCodes.CheckoutLocked,
                    $"{item.ProductName} is currently being purchased by another customer. Please try again in a few minutes.",
                    new Dictionary<string, object?> { ["productId"] = item.ProductId, ["retryAfterUtc"] = row?.ExpiresAtUtc });

            if (soonest is null || row.ExpiresAtUtc < soonest)
                soonest = row.ExpiresAtUtc;
        }

        return soonest;
    }

    public async Task ReleaseAsync(Guid cartId, CancellationToken ct)
    {
        try
        {
            await _db.CheckoutLocks.Where(l => l.OwnerCartId == cartId).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            // The lock expires on its own; never fail a placed order over it.
            _logger.LogWarning(ex, "Could not release the checkout locks of cart {CartId}.", cartId);
        }
    }
}
