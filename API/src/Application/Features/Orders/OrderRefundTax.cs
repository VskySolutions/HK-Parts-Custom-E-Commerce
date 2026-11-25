using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// The tax side of a refund that leaves the order's totals unchanged (admin Refund). The tax handed back is the
/// order's tax pro-rated to what is refunded, capped at the tax not yet refunded, so it can never exceed what the
/// order charged. It also builds the matching <see cref="TaxReturnRequest"/> for Avalara.
/// </summary>
public static class OrderRefundTax
{
    /// <summary>Tax charged and not yet handed back.</summary>
    public static decimal Remaining(Order order) => Math.Max(0m, order.TaxTotal - order.TaxRefundedTotal);

    /// <summary>The tax on refunded lines worth <paramref name="lineValue"/>: order tax × line value ÷ all lines' value.</summary>
    public static decimal ForLines(Order order, decimal lineValue)
    {
        var allLines = order.Lines.Sum(l => l.LineTotal);
        if (allLines <= 0m || lineValue <= 0m)
            return 0m;
        return Math.Min(Remaining(order), Round(order.TaxTotal * Math.Min(1m, lineValue / allLines)));
    }

    /// <summary>
    /// The tax inside a refund of <paramref name="refunded"/> money: all remaining tax for a full refund, otherwise
    /// order tax × refunded ÷ order total.
    /// </summary>
    public static decimal ForAmount(Order order, decimal refunded, bool full)
    {
        if (refunded <= 0m)
            return 0m;
        if (full)
            return Remaining(order);
        if (order.TotalAmount <= 0m)
            return 0m;
        return Math.Min(Remaining(order), Round(order.TaxTotal * Math.Min(1m, refunded / order.TotalAmount)));
    }

    /// <summary>A return of exactly the chosen lines, each at its value net of the order-level discount.</summary>
    public static TaxReturnRequest LinesReturn(Order order, IReadOnlySet<Guid> lineIds, decimal tax, string reference)
    {
        var ratio = DiscountRatio(order);
        var lines = order.Lines
            .Where(l => lineIds.Contains(l.Id))
            .Select(l => new TaxReturnLine(l.Id, l.Quantity, Round(l.LineTotal * ratio)))
            .ToList();
        return new TaxReturnRequest(order.Id, lines, 0m, tax, reference);
    }

    /// <summary>A return of a share of the whole order: every line and the shipping scaled by refunded ÷ order total.</summary>
    public static TaxReturnRequest ProportionalReturn(Order order, decimal refunded, decimal tax, string reference)
    {
        var share = order.TotalAmount > 0m ? Math.Min(1m, refunded / order.TotalAmount) : 1m;
        var ratio = DiscountRatio(order);
        var lines = order.Lines
            .Where(l => l.LineTotal > 0m)
            .Select(l => new TaxReturnLine(l.Id, Math.Round(l.Quantity * share, 4), Round(l.LineTotal * ratio * share)))
            .ToList();
        return new TaxReturnRequest(order.Id, lines, Round(order.ShippingTotal * share), tax, reference);
    }

    /// <summary>Share of the merchandise left after order-level discounts (tax was charged on the discounted value).</summary>
    private static decimal DiscountRatio(Order order)
        => order.Subtotal > 0m ? Math.Clamp((order.Subtotal - order.DiscountTotal) / order.Subtotal, 0m, 1m) : 1m;

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
