namespace HKPartsEcommerce.Domain.Enums;

/// <summary>How a store-credit ledger entry changed a customer's balance (WO-48, REQ-ORD-004).</summary>
public enum StoreCreditTransactionType
{
    /// <summary>Credit added to the balance (e.g. an approved store-credit return).</summary>
    Issue = 0,
    /// <summary>Credit spent from the balance (e.g. applied to an order).</summary>
    Redeem = 1,
    /// <summary>A manual admin correction (positive or negative).</summary>
    Adjustment = 2,
}
