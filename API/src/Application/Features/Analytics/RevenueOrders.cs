using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Analytics;

/// <summary>Shared revenue-recognition predicate used by the analytics dashboard and the operational reports.</summary>
public static class RevenueOrders
{
    /// <summary>
    /// Restricts an order query to <b>recognised revenue</b>: real orders whose money has actually been
    /// captured. It drops provisional (abandoned redirect-payment) shells via
    /// <see cref="OrderQueryExtensions.ExcludeUnpaidRedirect"/>, excludes <see cref="OrderStatus.Cancelled"/>,
    /// and keeps only orders that are <see cref="PaymentStatus.Captured"/> or
    /// <see cref="PaymentStatus.PartiallyRefunded"/> — the same "counts only captured money" rule the admin
    /// customer views use for lifetime value. Authorized-only, COD/awaiting-payment and failed orders are
    /// therefore excluded from revenue totals.
    /// </summary>
    public static IQueryable<Order> WithRecognisedRevenue(this IQueryable<Order> query) =>
        query
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries()
            .Where(o => o.Status != OrderStatus.Cancelled
                        && (o.PaymentStatus == PaymentStatus.Captured
                            || o.PaymentStatus == PaymentStatus.PartiallyRefunded));
}

/// <summary>
/// Guest checkout A-20/A-41: the customer type of an order. A guest order is one placed through guest checkout
/// (<c>checkout_method = 'guest'</c>): it has a guest record, or (older rows) neither an account nor an eBay id.
/// It stays a guest order after it is moved to an account; eBay imports are neither guest nor registered.
/// </summary>
public static class CustomerTypes
{
    public const string Guest = "Guest";
    public const string Registered = "Registered";

    public static IQueryable<Order> PlacedAsGuest(this IQueryable<Order> q) =>
        q.Where(o => o.GuestId != null || (o.CustomerId == null && o.EbayOrderId == null));

    public static IQueryable<Order> PlacedAsRegistered(this IQueryable<Order> q) =>
        q.Where(o => o.GuestId == null && o.CustomerId != null);

    /// <summary>Applies an "All" / "Guest" / "Registered" filter; anything else means All.</summary>
    public static IQueryable<Order> OfCustomerType(this IQueryable<Order> q, string? customerType) =>
        string.Equals(customerType, Guest, StringComparison.OrdinalIgnoreCase) ? q.PlacedAsGuest()
        : string.Equals(customerType, Registered, StringComparison.OrdinalIgnoreCase) ? q.PlacedAsRegistered()
        : q;
}
