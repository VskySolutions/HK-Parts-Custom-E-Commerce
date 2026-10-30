using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>
/// Shared guards for the buyer-facing subscription slices: confirm the acting customer owns a
/// subscription, and reload it (with product + customer) as a <see cref="SubscriptionDto"/>. A
/// subscription the caller does not own is reported as not found so the endpoint never leaks another
/// customer's subscription.
/// </summary>
internal static class SubscriptionOwnership
{
    public static async Task EnsureOwnedAsync(
        IApplicationDbContext db, ICurrentUserService current, Guid subscriptionId, CancellationToken ct)
    {
        if (current.UserId is not Guid userId)
            throw new UnauthorizedException();

        var customerId = await db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var owned = await db.Subscriptions
            .AsNoTracking()
            .AnyAsync(s => s.Id == subscriptionId && s.CustomerId == customerId, ct);
        if (!owned)
            throw new NotFoundException(nameof(Subscription), subscriptionId);
    }

    public static async Task<SubscriptionDto> LoadDtoAsync(
        IApplicationDbContext db, Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            ?? throw new NotFoundException(nameof(Subscription), subscriptionId);

        return SubscriptionDto.From(subscription);
    }
}
