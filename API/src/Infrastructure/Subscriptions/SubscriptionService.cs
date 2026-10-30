using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Infrastructure.Subscriptions;

/// <summary>
/// Recurring orders and subscription lifecycle (REQ-ORD-005). Lifecycle mutations (create / pause /
/// resume / change-interval / cancel) run on the caller's request-scoped <see cref="IApplicationDbContext"/>.
/// <see cref="GenerateDueOrdersAsync"/> runs on the background scheduler; it processes each due
/// subscription inside its <b>own</b> DI scope (fresh <see cref="IApplicationDbContext"/>) so one
/// subscription's failure is fully isolated — it cannot poison the others, nor the scheduler's own
/// context (which the worker shares to write its task log).
/// <para>
/// Limitation: no live recurring payment is captured — the framework persists no chargeable payment
/// instrument, only an opaque <c>PaymentMethodRef</c>. Generated orders are therefore created
/// <see cref="OrderStatus.Pending"/> / <see cref="PaymentStatus.Pending"/> (awaiting payment), matching
/// the project's "structurally-correct external flow, needs live credentials" convention. A future
/// recurring-capture integration hangs off this method, and a capture failure already flows through the
/// same pause-and-notify path implemented here (AC-ORD-005.5).
/// </para>
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        IApplicationDbContext db,
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider clock,
        ILogger<SubscriptionService> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(
        Guid customerId, Guid productId, Guid? variantId, int qty, SubscriptionInterval interval, Guid? addressId, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new NotFoundException(nameof(Product), productId);

        if (!product.IsSubscribable)
            throw new ValidationException(new[]
            {
                new ValidationFailure("productId", "This product is not available for subscription.")
            });

        var allowed = ParseIntervals(product.SubscriptionIntervals);
        if (!allowed.Contains(interval))
            throw new ValidationException(new[]
            {
                new ValidationFailure("interval", $"The '{interval}' recurrence interval is not available for this product.")
            });

        // Guard against a dangling variant so every future generated order snapshots a real variant.
        if (variantId is Guid vId)
        {
            var variantExists = await _db.ProductVariants.AsNoTracking()
                .AnyAsync(v => v.Id == vId && v.ProductId == productId, ct);
            if (!variantExists)
                throw new NotFoundException(nameof(ProductVariant), vId);
        }

        var now = _clock.UtcNow;
        var subscription = new Subscription
        {
            CustomerId = customerId,
            ProductId = productId,
            ProductVariantId = variantId,
            Quantity = qty <= 0 ? 1 : qty,
            Interval = interval,
            Status = SubscriptionStatus.Active,
            NextOrderOnUtc = NextDate(now, interval),
            ShippingAddressId = addressId,
            FailureCount = 0,
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);
        return subscription.Id;
    }

    public async Task PauseAsync(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await Load(subscriptionId, ct);
        if (subscription.Status == SubscriptionStatus.Cancelled)
            throw new ConflictException("A cancelled subscription cannot be paused.");

        subscription.Status = SubscriptionStatus.Paused;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResumeAsync(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await Load(subscriptionId, ct);
        if (subscription.Status == SubscriptionStatus.Cancelled)
            throw new ConflictException("A cancelled subscription cannot be resumed.");

        subscription.Status = SubscriptionStatus.Active;
        // A resumed subscription starts a fresh cycle: clear the failure streak (AC-ORD-005.5 — the buyer
        // updates their payment method, then resumes) and reschedule the next order from now.
        subscription.FailureCount = 0;
        subscription.NextOrderOnUtc = NextDate(_clock.UtcNow, subscription.Interval);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ChangeIntervalAsync(Guid subscriptionId, SubscriptionInterval interval, CancellationToken ct)
    {
        var subscription = await Load(subscriptionId, ct);
        if (subscription.Status == SubscriptionStatus.Cancelled)
            throw new ConflictException("A cancelled subscription cannot be modified.");

        subscription.Interval = interval;
        // Reschedule the next order onto the new cadence so it never lands in the past.
        subscription.NextOrderOnUtc = NextDate(_clock.UtcNow, interval);
        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await Load(subscriptionId, ct);
        subscription.Status = SubscriptionStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GenerateDueOrdersAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // Snapshot the due ids on an isolated read context, then process each in its own scope.
        List<Guid> dueIds;
        using (var readScope = _scopeFactory.CreateScope())
        {
            var readDb = readScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            dueIds = await readDb.Subscriptions.AsNoTracking()
                .Where(s => s.Status == SubscriptionStatus.Active && s.NextOrderOnUtc <= now)
                .OrderBy(s => s.NextOrderOnUtc)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        var generated = 0;
        foreach (var id in dueIds)
            if (await TryGenerateOneAsync(id, now, ct))
                generated++;

        return generated;
    }

    /// <summary>
    /// Generates the recurring order for a single subscription inside its own scope, so any failure is
    /// contained. On failure the subscription is paused (failure count incremented) and the subscriber is
    /// notified — the batch continues with the next subscription.
    /// </summary>
    private async Task<bool> TryGenerateOneAsync(Guid id, DateTime now, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var subscription = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
            // Re-check under a fresh read: status/schedule may have changed since the id snapshot.
            if (subscription is null
                || subscription.Status != SubscriptionStatus.Active
                || subscription.NextOrderOnUtc > now)
                return false;

            var product = await db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == subscription.ProductId, ct)
                ?? throw new NotFoundException(nameof(Product), subscription.ProductId);

            ProductVariant? variant = null;
            if (subscription.ProductVariantId is Guid variantId)
            {
                variant = await db.ProductVariants.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == product.Id, ct)
                    ?? throw new NotFoundException(nameof(ProductVariant), variantId);
            }

            var unitPrice = variant?.Price ?? product.Price ?? 0m;
            var lineTotal = unitPrice * subscription.Quantity;

            var order = new Order
            {
                // GUID-based number (not the checkout ORD-{timestamp} scheme): a batch run can generate
                // several orders in the same millisecond, which would collide on the unique OrderNumber index.
                OrderNumber = $"SUB-{Guid.NewGuid():N}",
                CustomerId = subscription.CustomerId,
                Status = OrderStatus.Pending,
                ShippingAddressId = subscription.ShippingAddressId,
                PlacedOnUtc = now,
                CurrencyCode = "USD",
                Subtotal = lineTotal,
                TotalAmount = lineTotal,
                PaymentStatus = PaymentStatus.Pending,
                Lines = new List<OrderLineItem>
                {
                    new()
                    {
                        ProductId = subscription.ProductId,
                        ProductVariantId = subscription.ProductVariantId,
                        ProductName = product.Name,
                        Sku = variant?.Sku ?? product.Sku,
                        Quantity = subscription.Quantity,
                        UnitPrice = unitPrice,
                        OriginalUnitPrice = unitPrice,
                        LineTotal = lineTotal,
                    }
                },
            };

            db.Orders.Add(order);
            subscription.LastOrderOnUtc = now;
            subscription.NextOrderOnUtc = NextDate(subscription.NextOrderOnUtc, subscription.Interval);
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SubscriptionService: failed to generate a recurring order for subscription {SubscriptionId}; pausing it.", id);
            await PauseAfterFailureAsync(id, ct);
            return false;
        }
    }

    /// <summary>Pauses a subscription after a failed recurring order and notifies the subscriber (AC-ORD-005.5).</summary>
    private async Task PauseAfterFailureAsync(Guid subscriptionId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var emails = scope.ServiceProvider.GetRequiredService<IEmailEnqueuer>();

            var subscription = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
            if (subscription is null)
                return;

            subscription.Status = SubscriptionStatus.Paused;
            subscription.FailureCount += 1;
            await db.SaveChangesAsync(ct);

            await NotifyPausedAsync(db, emails, subscription, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SubscriptionService: failed to pause/notify subscription {SubscriptionId} after a generation error.", subscriptionId);
        }
    }

    private async Task NotifyPausedAsync(IApplicationDbContext db, IEmailEnqueuer emails, Subscription subscription, CancellationToken ct)
    {
        var recipient = await db.Customers.AsNoTracking()
            .Where(c => c.Id == subscription.CustomerId)
            .Select(c => new { c.User!.Email, c.FirstName, c.LastName })
            .FirstOrDefaultAsync(ct);

        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
        {
            _logger.LogWarning(
                "SubscriptionService: no email on file for customer {CustomerId}; skipping paused-subscription notice.",
                subscription.CustomerId);
            return;
        }

        var productName = await db.Products.AsNoTracking()
            .Where(p => p.Id == subscription.ProductId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct) ?? "your subscription";

        var name = $"{recipient.FirstName} {recipient.LastName}".Trim();
        var body =
            $"Hi {(string.IsNullOrWhiteSpace(name) ? "there" : name)},\n\n" +
            $"We were unable to process the latest recurring order for your subscription to {productName}, " +
            "so we have paused it for now. Please review and update your payment method, then resume the " +
            "subscription from your account to continue receiving your orders.\n\nThank you.";

        await emails.EnqueueAsync(
            "subscription.paused",
            recipient.Email,
            string.IsNullOrWhiteSpace(name) ? null : name,
            "Your subscription has been paused",
            body,
            NotificationCategory.Transactional,
            cancellationToken: ct);
    }

    private async Task<Subscription> Load(Guid subscriptionId, CancellationToken ct)
        => await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
           ?? throw new NotFoundException(nameof(Subscription), subscriptionId);

    /// <summary>The set of intervals a product allows, parsed from its CSV (enum member names, case-insensitive).</summary>
    private static HashSet<SubscriptionInterval> ParseIntervals(string? csv)
    {
        var set = new HashSet<SubscriptionInterval>();
        if (string.IsNullOrWhiteSpace(csv))
            return set;

        foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<SubscriptionInterval>(token, ignoreCase: true, out var parsed))
                set.Add(parsed);

        return set;
    }

    /// <summary>Advances a date by one interval: Weekly +7d, BiWeekly +14d, Monthly +1mo, Quarterly +3mo.</summary>
    private static DateTime NextDate(DateTime from, SubscriptionInterval interval) => interval switch
    {
        SubscriptionInterval.Weekly => from.AddDays(7),
        SubscriptionInterval.BiWeekly => from.AddDays(14),
        SubscriptionInterval.Monthly => from.AddMonths(1),
        SubscriptionInterval.Quarterly => from.AddMonths(3),
        _ => from.AddMonths(1),
    };
}
