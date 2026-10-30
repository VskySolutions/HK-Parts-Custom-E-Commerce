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
