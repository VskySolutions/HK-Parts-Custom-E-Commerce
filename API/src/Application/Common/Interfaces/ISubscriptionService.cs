using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Recurring orders and subscription lifecycle (REQ-ORD-005). Creates subscriptions (validating product
/// subscribability and allowed intervals), processes subscriber pause/interval-change/cancel/resume
/// requests, and generates the due recurring orders on schedule for the background worker.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Creates an active subscription for <paramref name="customerId"/>. Validates the product exists, is
    /// subscribable, and that <paramref name="interval"/> is one of its admin-allowed intervals; sets the
    /// first <c>NextOrderOnUtc</c> to now + one interval. Returns the new subscription id.
    /// </summary>
    Task<Guid> CreateAsync(
        Guid customerId,
        Guid productId,
        Guid? variantId,
        int qty,
        SubscriptionInterval interval,
        Guid? addressId,
        CancellationToken ct);

    /// <summary>Pauses an active subscription so it stops generating orders.</summary>
    Task PauseAsync(Guid subscriptionId, CancellationToken ct);

    /// <summary>Resumes a paused subscription, clearing prior failures and recomputing NextOrderOnUtc from now.</summary>
    Task ResumeAsync(Guid subscriptionId, CancellationToken ct);

    /// <summary>Changes the recurrence interval and reschedules the next order onto the new cadence.</summary>
    Task ChangeIntervalAsync(Guid subscriptionId, SubscriptionInterval interval, CancellationToken ct);

    /// <summary>Cancels a subscription (terminal).</summary>
    Task CancelAsync(Guid subscriptionId, CancellationToken ct);

    /// <summary>
    /// Generates a Pending order for every Active subscription whose NextOrderOnUtc is due, advancing each
    /// by one interval. A per-subscription generation failure pauses that subscription, increments its
    /// failure count, and enqueues a "subscription paused" email — without aborting the batch. Returns the
    /// number of orders generated.
    /// </summary>
    Task<int> GenerateDueOrdersAsync(CancellationToken ct);
}
