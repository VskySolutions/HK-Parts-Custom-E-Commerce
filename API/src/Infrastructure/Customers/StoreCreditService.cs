using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Customers;

/// <summary>
/// The store-credit ledger (WO-48). A balance is the running sum of a customer's signed entries; issuing
/// appends a positive entry, redeeming a negative one (guarded against overdraw). The ledger is append-only.
/// </summary>
public class StoreCreditService : IStoreCreditService
{
    private readonly IApplicationDbContext _db;

    public StoreCreditService(IApplicationDbContext db) => _db = db;

    public async Task<decimal> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await _db.StoreCreditTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

    public async Task<Guid> IssueAsync(
        Guid customerId, decimal amount, string currencyCode, string? reason,
        Guid? rmaId = null, Guid? orderId = null, CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
            throw new ConflictException("The store-credit amount to issue must be greater than zero.");

        var entry = Append(customerId, amount, StoreCreditTransactionType.Issue, currencyCode, reason, rmaId, orderId);
        await _db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task<Guid> RedeemAsync(
        Guid customerId, decimal amount, string currencyCode, string? reason,
        Guid? orderId = null, CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
            throw new ConflictException("The store-credit amount to redeem must be greater than zero.");

        var balance = await GetBalanceAsync(customerId, cancellationToken);
        if (amount > balance)
            throw new ConflictException($"Insufficient store credit: the balance is {balance:0.00} but {amount:0.00} was requested.");

        var entry = Append(customerId, -amount, StoreCreditTransactionType.Redeem, currencyCode, reason, null, orderId);
        await _db.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    private StoreCreditTransaction Append(
        Guid customerId, decimal amount, StoreCreditTransactionType type, string currencyCode,
        string? reason, Guid? rmaId, Guid? orderId)
    {
        var entry = new StoreCreditTransaction
        {
            CustomerId = customerId,
            Amount = amount,
            Type = type,
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode,
            Reason = reason,
            RmaId = rmaId,
            OrderId = orderId,
        };
        _db.StoreCreditTransactions.Add(entry);
        return entry;
    }
}
