using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>Validator rules for the bulk block (no database).</summary>
public class BulkBlacklistCustomersValidatorTests
{
    private static readonly BulkBlacklistCustomersCommandValidator Validator = new();

    [Fact]
    public void Ids_are_required_unless_all_matching()
    {
        Assert.False(Validator.Validate(new BulkBlacklistCustomersCommand(null, false, null, "Fraud", null)).IsValid);
        Assert.False(Validator.Validate(new BulkBlacklistCustomersCommand(new(), false, null, "Fraud", null)).IsValid);
        Assert.True(Validator.Validate(new BulkBlacklistCustomersCommand(null, true, null, "Fraud", null)).IsValid);
    }

    [Fact]
    public void Reason_is_required()
        => Assert.False(Validator.Validate(new BulkBlacklistCustomersCommand(new() { Guid.NewGuid() }, false, null, "  ", null)).IsValid);
}

/// <summary>Bulk blocking from the Customers list. Real SQL Server test DB.</summary>
public class BulkBlacklistCustomersTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private Guid SeedCustomer(string firstName, string lastName, bool asStaff = false)
    {
        using var db = NewContext();
        Role FindOrCreate(string name) => db.Roles.FirstOrDefault(r => r.Name == name)
            ?? new Role { Name = name, NormalizedName = name.ToUpperInvariant(), IsSystemRole = true };

        var email = $"{Guid.NewGuid():N}@test.local";
        var user = new User
        {
            Username = email, Email = email, PasswordHash = "x", EmailVerified = true, IsActive = true,
            Customer = new Customer { FirstName = firstName, LastName = lastName },
        };
        user.UserRoles.Add(new UserRole { Role = FindOrCreate(nameof(RoleType.Customer)), AssignedOnUtc = Now });
        if (asStaff)
            user.UserRoles.Add(new UserRole { Role = FindOrCreate(nameof(RoleType.TenantAdmin)), AssignedOnUtc = Now });
        db.Users.Add(user);
        db.SaveChanges();
        return user.Customer!.Id;
    }

    private AppDbContext AdminContext()
    {
        var db = new AppDbContext(_options, new FakeCurrentUser { UserId = Guid.NewGuid() }, new FixedClock(Now));
        db.Database.UseTransaction(_transaction);
        return db;
    }

    private async Task<BulkBlacklistCustomersResult> Bulk(BulkBlacklistCustomersCommand cmd)
    {
        using var db = AdminContext();
        return await new BulkBlacklistCustomersCommandHandler(db).Handle(cmd, CancellationToken.None);
    }

    [Fact]
    public async Task Blocks_selected_ids_and_skips_staff_unknown_and_already_blocked()
    {
        var a = SeedCustomer("BulkA", "Sel");
        var b = SeedCustomer("BulkB", "Sel");
        var staff = SeedCustomer("BulkStaff", "Sel", asStaff: true);
        var untouched = SeedCustomer("BulkC", "Sel");

        var first = await Bulk(new(new() { a, b, staff, Guid.NewGuid() }, false, null, "Chargeback", "note"));
        Assert.Equal(2, first.Blocked);
        Assert.Equal(2, first.Skipped);

        // Blocking again skips them rather than failing.
        var again = await Bulk(new(new() { a }, false, null, "Chargeback", null));
        Assert.Equal(0, again.Blocked);
        Assert.Equal(1, again.Skipped);

        using var db = NewContext();
        var blocked = await db.BlacklistedCustomers.Select(x => x.CustomerId).ToListAsync();
        Assert.Contains(a, blocked);
        Assert.Contains(b, blocked);
        Assert.DoesNotContain(staff, blocked);
        Assert.DoesNotContain(untouched, blocked);

        var entries = await db.CustomerBlacklistEntries.Where(e => e.CustomerId == a || e.CustomerId == b).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal(CustomerBlacklistStatus.Blocked, e.Status));
        Assert.All(entries, e => Assert.Equal("Chargeback", e.Reason));
    }

    [Fact]
    public async Task Reblocking_after_unblock_reuses_the_entry_and_records_each_block()
    {
        var c = SeedCustomer("Times", "Blocked");
        await Bulk(new(new() { c }, false, null, "First", null));

        Guid entryId;
        using (var db = NewContext())
            entryId = (await db.CustomerBlacklistEntries.SingleAsync(e => e.CustomerId == c)).Id;

        // Unblock from the Blocked customers page, then block again from the Customers list.
        using (var db = AdminContext())
        {
            var e = await db.CustomerBlacklistEntries.AsNoTracking().SingleAsync(x => x.Id == entryId);
            await new HKPartsEcommerce.Application.Features.Checkout.SaveCustomerBlacklistEntryCommandHandler(db).Handle(
                new(entryId, e.FirstName, e.LastName, e.Email, e.Reason, CustomerBlacklistStatus.Inactive), CancellationToken.None);
        }
        await Bulk(new(new() { c }, false, null, "Second", null));

        // The re-block reuses the customer's entry and the history records both blocks.
        using var read = NewContext();
        var entry = await read.CustomerBlacklistEntries.SingleAsync(e => e.CustomerId == c);
        Assert.Equal(entryId, entry.Id);
        Assert.Equal(CustomerBlacklistStatus.Blocked, entry.Status);
        Assert.Equal(2, await read.CustomerBlacklistEvents.CountAsync(
            e => e.BlacklistEntryId == entryId && e.Action == CustomerBlacklistAction.Blocked));
    }

    [Fact]
    public async Task All_matching_blocks_every_customer_matching_the_filter_only()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var m1 = SeedCustomer("Match", tag);
        var m2 = SeedCustomer("Match", tag);
        var other = SeedCustomer("Other", "Person");

        var result = await Bulk(new(null, true, new CustomerListFilter(Search: tag), "Fraud", null));
        Assert.Equal(2, result.Blocked);

        using var db = NewContext();
        var blocked = await db.BlacklistedCustomers.Select(x => x.CustomerId).ToListAsync();
        Assert.Contains(m1, blocked);
        Assert.Contains(m2, blocked);
        Assert.DoesNotContain(other, blocked);

        // They drop out of the Customers list.
        var list = await new ListCustomersQueryHandler(db).Handle(new ListCustomersQuery(Search: tag), CancellationToken.None);
        Assert.Equal(0, list.TotalCount);
    }
}
