namespace HKPartsEcommerce.Domain.Enums;

/// <summary>How a loyalty-points ledger entry changed a customer's points balance (WO-27).</summary>
public enum PointsTransactionType
{
    /// <summary>Points credited for a placed + paid order (order total × earn rate, floored).</summary>
    Earned = 0,
    /// <summary>Points spent as a checkout discount (applied to a cart, redeemed at placement).</summary>
    Redeemed = 1,
    /// <summary>Points clawed back when an order that earned them is refunded.</summary>
    RefundReversal = 2,
    /// <summary>A manual admin correction (positive or negative).</summary>
    Adjustment = 3,
}
