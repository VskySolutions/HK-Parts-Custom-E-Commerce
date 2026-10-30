namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// The store-credit ledger (WO-48, REQ-ORD-004): issue, redeem and read a customer's balance. A balance
/// is the sum of the customer's signed ledger entries; every mutation is an append (the ledger is immutable).
/// </summary>
public interface IStoreCreditService
{
    /// <summary>The customer's current store-credit balance (sum of their ledger entries).</summary>
    Task<decimal> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Credits the customer's balance (e.g. an approved store-credit return) and returns the entry id.</summary>
    Task<Guid> IssueAsync(
        Guid customerId, decimal amount, string currencyCode, string? reason,
        Guid? rmaId = null, Guid? orderId = null, CancellationToken cancellationToken = default);

    /// <summary>Debits the customer's balance (e.g. credit applied to an order); throws if the balance is insufficient.</summary>
    Task<Guid> RedeemAsync(
        Guid customerId, decimal amount, string currencyCode, string? reason,
        Guid? orderId = null, CancellationToken cancellationToken = default);
}
