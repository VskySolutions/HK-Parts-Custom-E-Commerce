using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Infrastructure.Customers;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>
/// The store-credit ledger (WO-48 / REQ-ORD-004): a balance is the running sum of signed entries;
/// issuing credits, redeeming debits (guarded against overdraw). Backed by the real SQL Server test DB.
/// </summary>
public class StoreCreditServiceTests : CatalogTestBase
{
    /// <summary>Seeds a User + Customer (the ledger FK requires a real customer) and returns the customer id.</summary>
    private Guid SeedCustomer()
    {
        using var db = NewContext();
        var user = new User
        {
            Username = $"u{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@test.local",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        var customer = new Customer { UserId = user.Id, FirstName = "Test", LastName = "Buyer" };
        db.Customers.Add(customer);
        db.SaveChanges();
        return customer.Id;
    }

    [Fact]
    public async Task Issue_IncreasesBalance()
    {
        var customerId = SeedCustomer();
        var svc = new StoreCreditService(NewContext());

        await svc.IssueAsync(customerId, 25m, "USD", "test grant");

        Assert.Equal(25m, await svc.GetBalanceAsync(customerId));
    }

    [Fact]
    public async Task Issues_Accumulate()
    {
        var customerId = SeedCustomer();
        var svc = new StoreCreditService(NewContext());

        await svc.IssueAsync(customerId, 10m, "USD", "a");
        await svc.IssueAsync(customerId, 15m, "USD", "b");

        Assert.Equal(25m, await svc.GetBalanceAsync(customerId));
    }

    [Fact]
    public async Task Redeem_DecreasesBalance()
    {
        var customerId = SeedCustomer();
        var svc = new StoreCreditService(NewContext());

        await svc.IssueAsync(customerId, 40m, "USD", "grant");
        await svc.RedeemAsync(customerId, 12m, "USD", "spend");

        Assert.Equal(28m, await svc.GetBalanceAsync(customerId));
    }

    [Fact]
    public async Task Redeem_BeyondBalance_Throws()
    {
        var customerId = SeedCustomer();
        var svc = new StoreCreditService(NewContext());

        await svc.IssueAsync(customerId, 5m, "USD", "grant");

        await Assert.ThrowsAsync<ConflictException>(() => svc.RedeemAsync(customerId, 10m, "USD", "overdraw"));
    }

    [Fact]
    public async Task Issue_NonPositive_Throws()
    {
        var svc = new StoreCreditService(NewContext());

        await Assert.ThrowsAsync<ConflictException>(() => svc.IssueAsync(Guid.NewGuid(), 0m, "USD", "bad"));
    }

    [Fact]
    public async Task Balance_IsZero_ForCustomerWithNoLedger()
    {
        var svc = new StoreCreditService(NewContext());

        Assert.Equal(0m, await svc.GetBalanceAsync(Guid.NewGuid()));
    }
}
