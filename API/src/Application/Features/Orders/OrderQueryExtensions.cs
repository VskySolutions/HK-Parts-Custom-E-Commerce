using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>Shared query predicates for orders.</summary>
public static class OrderQueryExtensions
{
    /// <summary>
    /// Excludes <b>provisional</b> orders — ones created only to hold a redirect (Stripe Checkout) payment
    /// session that was never paid (the buyer cancelled or abandoned payment on the gateway). Such orders
    /// carry a <see cref="Order.SourceCartId"/> and are still <see cref="PaymentStatus.Pending"/>; once
    /// payment is captured they no longer match and appear in listings normally. Applied to the admin,
    /// customer and store-queue lists so mere payment attempts don't look like real orders — while the
    /// orders stay loadable by id for the checkout confirm/retry flow (hence no global query filter).
    /// </summary>
    public static IQueryable<Order> ExcludeUnpaidRedirect(this IQueryable<Order> query)
        => query.Where(o => o.SourceCartId == null || o.PaymentStatus != PaymentStatus.Pending);

    /// <summary>
    /// Excludes inquiries (quote requests, REQ-INQ-001). They are stored as orders so they reuse lines,
    /// addresses, routing and status history, but they are not sales: nothing was paid, no stock moved.
    /// Apply this to every order listing, customer order count and revenue/analytics query — an inquiry
    /// carries a realistic total and would visibly inflate any of them.
    /// </summary>
    public static IQueryable<Order> ExcludeInquiries(this IQueryable<Order> query)
        => query.Where(o => !o.IsInquiry);

    /// <summary>Narrows to inquiries only — the admin Inquiries list and the buyer's My Inquiries page.</summary>
    public static IQueryable<Order> OnlyInquiries(this IQueryable<Order> query)
        => query.Where(o => o.IsInquiry);
}
