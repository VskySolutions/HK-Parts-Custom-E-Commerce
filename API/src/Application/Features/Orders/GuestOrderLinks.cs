using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Signed guest order links (guest checkout U-52, U-53, U-61): which orders get one, and how long it lasts.
/// A guest order is a real storefront order with no account: not an eBay import, not an inquiry.
/// </summary>
public static class GuestOrderLinks
{
    public static IQueryable<Order> GuestOrders(this IQueryable<Order> orders) =>
        orders.Where(o => o.CustomerId == null && o.EbayOrderId == null && !o.IsInquiry);

    /// <summary>A token for <paramref name="orderId"/> when it is a guest order; null otherwise.</summary>
    public static async Task<string?> TokenForAsync(
        IApplicationDbContext db, IGuestOrderTokenService tokens, ISettingsService settings, Guid orderId, CancellationToken ct)
    {
        if (!await db.Orders.AsNoTracking().GuestOrders().AnyAsync(o => o.Id == orderId, ct))
            return null;
        var guest = await GuestCheckoutSettingsStore.LoadAsync(settings, ct);
        return tokens.Generate(orderId, TimeSpan.FromDays(guest.LinkLifetimeDays));
    }

    /// <summary>Adds the guest order token to a successful checkout result (confirmation page: view order, create account).</summary>
    public static async Task<CheckoutResult> WithGuestTokenAsync(
        this CheckoutResult result, IApplicationDbContext db, IGuestOrderTokenService tokens, ISettingsService settings,
        ICurrentUserService current, CancellationToken ct)
    {
        if (result.Success && result.OrderId != Guid.Empty && current.UserId is null)
            result.GuestOrderToken = await TokenForAsync(db, tokens, settings, result.OrderId, ct);
        return result;
    }
}
