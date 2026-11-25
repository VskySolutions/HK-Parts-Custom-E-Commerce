using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Guest records (docs/guest-checkout-requirements.md §5.2): the one place that creates / refreshes a
/// <see cref="Guest"/> row for a guest order and moves a guest's orders onto a customer account. Changes are
/// staged only; the caller saves.
/// </summary>
public static class GuestRecords
{
    /// <summary>
    /// The guest row for <paramref name="email"/> (normalized), created when missing, with name and phone
    /// refreshed from this order (the list shows the latest order's details). An opt-in is never undone here:
    /// an unticked box on a later order does not withdraw an earlier consent.
    /// </summary>
    public static async Task<Guest?> UpsertAsync(
        IApplicationDbContext db, string? email, string? firstName, string? lastName, string? phone,
        bool marketingOptIn, DateTime now, CancellationToken ct)
    {
        var normalized = Guest.NormalizeEmail(email);
        if (normalized.Length == 0)
            return null;

        var guest = db.Guests.Local.FirstOrDefault(g => g.EmailNormalized == normalized)
                    ?? await db.Guests.FirstOrDefaultAsync(g => g.EmailNormalized == normalized, ct);
        if (guest is null)
        {
            guest = new Guest { EmailNormalized = normalized };
            db.Guests.Add(guest);
        }

        guest.FirstName = Trim(firstName, 200) ?? guest.FirstName;
        guest.LastName = Trim(lastName, 200) ?? guest.LastName;
        guest.PhoneNumber = Trim(phone, 50) ?? guest.PhoneNumber;
        guest.LastOrderOnUtc = now;
        if (marketingOptIn && !guest.MarketingOptIn)
        {
            guest.MarketingOptIn = true;
            guest.OptInOnUtc = now;
        }
        return guest;
    }

    /// <summary>
    /// Records a guest's "Email me news and offers" consent (U-14, N-06) as a newsletter subscription with
    /// source "checkout", so marketing sends (and their unsubscribe links) go through the one subscriber list.
    /// </summary>
    public static async Task SubscribeToNewsletterAsync(IApplicationDbContext db, string email, DateTime now, CancellationToken ct)
    {
        var normalized = Guest.NormalizeEmail(email);
        if (normalized.Length == 0)
            return;
        var existing = await db.CMSNewsletterSubscriptions.FirstOrDefaultAsync(s => s.Email == normalized, ct);
        if (existing is null)
        {
            db.CMSNewsletterSubscriptions.Add(new CMSNewsletterSubscription
            {
                Email = normalized,
                Status = NewsletterSubscriptionStatus.Subscribed,
                ConfirmedOnUtc = now,
                Source = "checkout",
            });
        }
        else if (existing.Status != NewsletterSubscriptionStatus.Subscribed)
        {
            existing.Status = NewsletterSubscriptionStatus.Subscribed;
            existing.ConfirmedOnUtc = now;
            existing.UnsubscribedOnUtc = null;
        }
    }

    /// <summary>
    /// Moves every guest order of <paramref name="email"/> onto <paramref name="customerId"/> (U-71 / A-18) and
    /// marks the guest row linked. Matches the guest id and, for orders placed before guest rows existed, the
    /// normalized contact email. Only real storefront orders move (no eBay imports, no inquiries). Returns how
    /// many orders moved. The caller must only call this once the email is verified or an admin confirmed (N-07).
    /// </summary>
    public static async Task<int> LinkOrdersAsync(
        IApplicationDbContext db, string email, Guid customerId, DateTime now, CancellationToken ct, Guid? changedById = null)
    {
        var normalized = Guest.NormalizeEmail(email);
        if (normalized.Length == 0)
            return 0;

        var guest = await db.Guests.FirstOrDefaultAsync(g => g.EmailNormalized == normalized, ct);
        var guestId = guest?.Id;
        var orders = await db.Orders
            .Where(o => o.CustomerId == null && o.EbayOrderId == null && !o.IsInquiry
                        && ((guestId != null && o.GuestId == guestId)
                            || (o.ShippingAddress != null && o.ShippingAddress.Email != null
                                && o.ShippingAddress.Email.Trim().ToLower() == normalized)))
            .ToListAsync(ct);

        foreach (var order in orders)
        {
            order.CustomerId = customerId;
            order.GuestId ??= guestId;
            // N-12: every link is written to the order's history, with the admin when an admin linked it.
            db.OrderStatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = order.Status,
                ToStatus = order.Status,
                ChangedById = changedById,
                ChangedOnUtc = now,
                Note = "Guest order moved to the customer's account.",
            });
        }

        if (guest is not null)
        {
            guest.LinkedCustomerId = customerId;
            guest.LinkedOnUtc = now;
        }
        return orders.Count;
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var t = value.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
