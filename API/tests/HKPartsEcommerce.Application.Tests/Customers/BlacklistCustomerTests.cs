using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>Validator rules for blacklisting (no database).</summary>
public class BlacklistCustomerValidatorTests
{
    private static readonly BlacklistCustomerCommandValidator Validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reason_is_required_after_trimming(string? reason)
    {
        var result = Validator.Validate(new BlacklistCustomerCommand(Guid.NewGuid(), reason, null));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BlacklistCustomerCommand.Reason));
    }

    [Fact]
    public void Enforces_column_max_lengths_on_trimmed_values()
    {
        var okReason = "  " + new string('r', BlacklistedCustomer.ReasonMaxLength) + "  ";
        Assert.True(Validator.Validate(new BlacklistCustomerCommand(Guid.NewGuid(), okReason, null)).IsValid);

        var result = Validator.Validate(new BlacklistCustomerCommand(
            Guid.NewGuid(),
            new string('r', BlacklistedCustomer.ReasonMaxLength + 1),
            new string('n', BlacklistedCustomer.InternalNotesMaxLength + 1)));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BlacklistCustomerCommand.Reason));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BlacklistCustomerCommand.InternalNotes));
    }

    [Fact]
    public void Customer_id_is_required()
        => Assert.Contains(Validator.Validate(new BlacklistCustomerCommand(Guid.Empty, "Fraud", null)).Errors,
            e => e.PropertyName == nameof(BlacklistCustomerCommand.CustomerId));
}

/// <summary>Blacklisting a customer and its effect on the admin Customers list. Real SQL Server test DB.</summary>
public class BlacklistCustomerTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    private Guid SeedCustomer(string firstName, bool asStaff = false)
    {
        using var db = NewContext();
        Role FindOrCreate(string name) => db.Roles.FirstOrDefault(r => r.Name == name)
            ?? new Role { Name = name, NormalizedName = name.ToUpperInvariant(), IsSystemRole = true };

        var email = $"{Guid.NewGuid():N}@test.local";
        var user = new User
        {
            Username = email, Email = email, PasswordHash = "x", EmailVerified = true, IsActive = true,
            Customer = new Customer { FirstName = firstName, LastName = "Blk" },
        };
        user.UserRoles.Add(new UserRole { Role = FindOrCreate(nameof(RoleType.Customer)), AssignedOnUtc = Now });
        if (asStaff)
            user.UserRoles.Add(new UserRole { Role = FindOrCreate(nameof(RoleType.TenantAdmin)), AssignedOnUtc = Now });
        db.Users.Add(user);
        db.SaveChanges();
        return user.Customer!.Id;
    }

    private AppDbContext AdminContext(Guid adminId)
    {
        var db = new AppDbContext(_options, new FakeCurrentUser { UserId = adminId }, new FixedClock(Now));
        db.Database.UseTransaction(_transaction);
        return db;
    }

    private async Task<Guid> Block(Guid customerId, string reason = "Chargeback", string? notes = null, Guid? adminId = null)
    {
        using var db = AdminContext(adminId ?? Guid.NewGuid());
        return await new BlacklistCustomerCommandHandler(db)
            .Handle(new BlacklistCustomerCommand(customerId, reason, notes), CancellationToken.None);
    }

    private async Task<HKPartsEcommerce.Application.Common.Models.PaginatedList<CustomerListItemDto>> ListCustomers(string search)
    {
        using var db = NewContext();
        return await new ListCustomersQueryHandler(db).Handle(new ListCustomersQuery(Search: search), CancellationToken.None);
    }

    [Fact]
    public async Task Block_saves_a_trimmed_record_stamped_with_the_admin_and_leaves_the_customer_untouched()
    {
        var customerId = SeedCustomer("Blocked");
        var adminId = Guid.NewGuid();

        var id = await Block(customerId, "  Fraudulent order  ", "  card flagged  ", adminId);

        using var db = NewContext();
        var row = await db.BlacklistedCustomers.AsNoTracking().SingleAsync(b => b.Id == id);
        Assert.Equal(customerId, row.CustomerId);
        Assert.Equal("Fraudulent order", row.Reason);
        Assert.Equal("card flagged", row.InternalNotes);
        Assert.False(row.Deleted);
        Assert.Equal(adminId, row.CreatedById);
        Assert.Equal(Now, row.CreatedOnUtc);
        Assert.Equal(Now, row.UpdatedOnUtc);

        var customer = await db.Customers.AsNoTracking().Include(c => c.User).SingleAsync(c => c.Id == customerId);
        Assert.False(customer.Deleted);
        Assert.True(customer.User!.IsActive);
    }

    [Fact]
    public async Task Blank_internal_notes_are_stored_as_null()
    {
        var id = await Block(SeedCustomer("Notes"), notes: "   ");
        using var db = NewContext();
        Assert.Null((await db.BlacklistedCustomers.AsNoTracking().SingleAsync(b => b.Id == id)).InternalNotes);
    }

    [Fact]
    public async Task Blocking_an_already_blacklisted_customer_is_a_conflict()
    {
        var customerId = SeedCustomer("Twice");
        await Block(customerId);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Block(customerId, "Again"));
        Assert.Equal(BlacklistCustomerCommandHandler.AlreadyBlacklistedMessage, ex.Message);
        using var db = NewContext();
        Assert.Equal(1, await db.BlacklistedCustomers.CountAsync(b => b.CustomerId == customerId));
    }

    [Fact]
    public async Task Database_allows_only_one_active_record_but_keeps_soft_deleted_history()
    {
        var customerId = SeedCustomer("Index");
        await Block(customerId);

        // A reversed (soft-deleted) block does not prevent a new one...
        using (var db = NewContext())
        {
            db.BlacklistedCustomers.Remove(await db.BlacklistedCustomers.SingleAsync(b => b.CustomerId == customerId));
            await db.SaveChangesAsync();
        }
        await Block(customerId, "Re-blocked");

        // ...but a second ACTIVE row is rejected by the filtered unique index.
        using (var db = NewContext())
        {
            db.BlacklistedCustomers.Add(new BlacklistedCustomer { CustomerId = customerId, Reason = "dup" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        using var check = NewContext();
        Assert.Equal(2, await check.BlacklistedCustomers.IgnoreQueryFilters().CountAsync(b => b.CustomerId == customerId));
    }

    [Fact]
    public async Task Unknown_customer_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Block(Guid.NewGuid()));

    [Fact]
    public async Task Staff_account_cannot_be_blacklisted()
        => await Assert.ThrowsAsync<ConflictException>(() => Block(SeedCustomer("Staff", asStaff: true)));

    [Fact]
    public async Task Blacklisted_customer_is_excluded_from_the_list_and_its_count()
    {
        var tag = $"T{Guid.NewGuid():N}"[..12];
        var blocked = SeedCustomer(tag + "a");
        var kept = SeedCustomer(tag + "b");

        var before = await ListCustomers(tag);
        Assert.Equal(2, before.TotalCount);

        await Block(blocked);

        var after = await ListCustomers(tag);
        Assert.Equal(1, after.TotalCount);
        Assert.Equal(kept, Assert.Single(after.Items).Id);
    }

    [Fact]
    public async Task Reversed_block_brings_the_customer_back_to_the_list()
    {
        var tag = $"R{Guid.NewGuid():N}"[..12];
        var customerId = SeedCustomer(tag);
        await Block(customerId);
        using (var db = NewContext())
        {
            db.BlacklistedCustomers.Remove(await db.BlacklistedCustomers.SingleAsync(b => b.CustomerId == customerId));
            await db.SaveChangesAsync();
        }

        Assert.Equal(customerId, Assert.Single((await ListCustomers(tag)).Items).Id);
    }
}
