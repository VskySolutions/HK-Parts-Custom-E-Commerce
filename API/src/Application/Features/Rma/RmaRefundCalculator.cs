using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>The refund breakdown of one return (section 10.1).</summary>
public class RmaRefundBreakdown
{
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal RestockFee { get; set; }
    public decimal LabelDeduction { get; set; }
    public decimal Adjustment { get; set; }
    public decimal Total { get; set; }
    /// <summary>True when the amounts use requested quantities (before inspection) — "final amount confirmed after inspection".</summary>
    public bool IsEstimate { get; set; }
}

/// <summary>
/// Computes the refund for an RMA from the order it belongs to:
/// <code>
/// line refund     = qty × unit paid (order-level discounts pro-rated) − inspection deduction
/// tax refund      = order tax × (returned line value ÷ order line value)
/// shipping refund = original shipping (minus shipping already refunded by earlier returns) when staff choose it,
///                   or when every unit of the order comes back for a merchant-fault reason
/// restocking fee  = fee % × line refund, for fee-applicable reasons (waivable)
/// label deduction = label cost when the policy deducts it and no line is merchant fault
/// total           = lines + tax + shipping − fee − label + adjustment, never below 0
/// </code>
/// Writes the per-line <see cref="RmaLineItem.UnitRefund"/> / <see cref="RmaLineItem.LineRefund"/> when
/// <paramref name="persist"/> is set (inspection), otherwise only returns the estimate.
/// </summary>
public static class RmaRefundCalculator
{
    public static RmaRefundBreakdown Calculate(
        Domain.Entities.Rma rma, Order order, ReturnSettingsDto settings,
        IReadOnlyDictionary<Guid, int> claimedByOtherRmas, decimal shippingRefundedByOtherRmas, bool persist)
    {
        var estimate = !RmaWorkflow.Settled.Contains(rma.Status) && rma.Status != RmaStatus.Received;
        var unitPaid = ReturnEligibility.UnitPaidByLine(order);
        var linesById = order.Lines.ToDictionary(l => l.Id);
        var linesTotal = order.Lines.Sum(l => l.LineTotal);

        decimal subtotal = 0, feeBase = 0, returnedLineValue = 0;
        var anyMerchantFault = false;
        var allMerchantFault = rma.Lines.Count > 0;

        foreach (var line in rma.Lines)
        {
            var qty = QuantityForRefund(line, rma.Status);
            var unit = unitPaid.GetValueOrDefault(line.OrderLineItemId);
            var lineRefund = Math.Max(0m, Round(qty * unit - (estimate ? 0m : line.Deduction)));

            if (persist)
            {
                line.UnitRefund = unit;
                line.LineRefund = lineRefund;
            }

            subtotal += lineRefund;
            if (line.FeeApplies && !line.MerchantFault)
                feeBase += lineRefund;
            if (line.MerchantFault && qty > 0)
                anyMerchantFault = true;
            if (!line.MerchantFault)
                allMerchantFault = false;

            if (linesById.TryGetValue(line.OrderLineItemId, out var orderLine) && orderLine.Quantity > 0)
                returnedLineValue += orderLine.LineTotal * qty / orderLine.Quantity;
        }

        var tax = linesTotal > 0 ? Round(order.TaxTotal * Math.Min(1m, returnedLineValue / linesTotal)) : 0m;

        // Whole order back (counting earlier returns) for a merchant-fault reason → shipping goes back too.
        var wholeOrderBack = order.Lines.Count > 0 && order.Lines.All(ol =>
            claimedByOtherRmas.GetValueOrDefault(ol.Id)
            + rma.Lines.Where(l => l.OrderLineItemId == ol.Id).Sum(l => QuantityForRefund(l, rma.Status)) >= ol.Quantity);
        var refundShipping = rma.RefundShipping || (wholeOrderBack && allMerchantFault);
        var shipping = refundShipping ? Math.Max(0m, order.ShippingTotal - shippingRefundedByOtherRmas) : 0m;

        var restockFee = rma.WaiveRestockFee ? 0m : Round(feeBase * settings.RestockFeePercent / 100m);

        var labelDeduction = settings.LabelMode == ReturnLabelMode.PrepaidDeducted && !anyMerchantFault && rma.LabelCost is > 0
            ? rma.LabelCost.Value
            : 0m;

        var adjustment = estimate ? 0m : rma.Adjustment;
        var total = Math.Max(0m, subtotal + tax + shipping - restockFee - labelDeduction + adjustment);

        return new RmaRefundBreakdown
        {
            Subtotal = subtotal,
            Tax = tax,
            Shipping = shipping,
            RestockFee = restockFee,
            LabelDeduction = labelDeduction,
            Adjustment = adjustment,
            Total = Round(total),
            IsEstimate = estimate,
        };
    }

    /// <summary>Copies a computed breakdown onto the RMA (frozen at inspection, re-frozen on re-inspection).</summary>
    public static void Apply(Domain.Entities.Rma rma, RmaRefundBreakdown b)
    {
        rma.SubtotalRefund = b.Subtotal;
        rma.TaxRefund = b.Tax;
        rma.ShippingRefund = b.Shipping;
        rma.RestockFee = b.RestockFee;
        rma.LabelDeduction = b.LabelDeduction;
        rma.TotalRefund = b.Total;
    }

    /// <summary>Requested quantity before receipt, received-and-accepted afterwards.</summary>
    public static int QuantityForRefund(RmaLineItem line, RmaStatus status)
        => status is RmaStatus.Requested or RmaStatus.Approved or RmaStatus.InTransit
            ? line.Quantity
            : line.QuantityAccepted;

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
