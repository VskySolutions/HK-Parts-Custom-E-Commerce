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
    private static readonly UnblockCustomerCommandValidator UnblockValidator = new();

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

    [Fact]
    public void Unblock_customer_id_is_required_and_notes_are_length_limited()
    {
        Assert.Contains(UnblockValidator.Validate(new UnblockCustomerCommand(Guid.Empty, null)).Errors,
            e => e.PropertyName == nameof(UnblockCustomerCommand.CustomerId));

        var tooLong = new UnblockCustomerCommand(Guid.NewGuid(), new string('n', BlacklistedCustomer.InternalNotesMaxLength + 1));
        Assert.Contains(UnblockValidator.Validate(tooLong).Errors,
            e => e.PropertyName == nameof(UnblockCustomerCommand.Notes));

        Assert.True(UnblockValidator.Validate(new UnblockCustomerCommand(Guid.NewGuid(), null)).IsValid);
    }
}

/// <summary>Blacklisting/unblocking a customer and its effect on the admin Customers list. Real SQL Server test DB.</summary>
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

    private async Task<Guid> Unblock(Guid customerId, string? notes = null, Guid? adminId = null)
    {
        using var db = AdminContext(adminId ?? Guid.NewGuid());
        return await new UnblockCustomerCommandHandler(db)
            .Handle(new UnblockCustomerCommand(customerId, notes), CancellationToken.None);
    }

    private async Task<HKPartsEcommerce.Application.Common.Models.PaginatedList<CustomerListItemDto>> ListCustomers(string search)
    {
        using var db = NewContext();
        return await new ListCustomersQueryHandler(db).Handle(new ListCustomersQuery(Search: search), CancellationToken.None);
    }

    [Fact]
    public async Task Block_sets_the_flag_and_appends_one_blocked_history_row()
    {
        var customerId = SeedCustomer("Blocked");
        var adminId = Guid.NewGuid();

        var id = await Block(customerId, "  Fraudulent order  ", "  card flagged  ", adminId);

        using var db = NewContext();
        var customer = await db.Customers.AsNoTracking().Include(c => c.User).SingleAsync(c => c.Id == customerId);
        Assert.True(customer.IsBlacklisted);
        Assert.False(customer.Deleted);
        Assert.True(customer.User!.IsActive);

        var row = await db.BlacklistedCustomers.AsNoTracking().SingleAsync(b => b.Id == id);
        Assert.Equal(customerId, row.CustomerId);
        Assert.Equal(BlacklistAction.Blocked, row.Action);
        Assert.Equal("Fraudulent order", row.Reason);
        Assert.Equal("card flagged", row.InternalNotes);
        Assert.Equal(adminId, row.CreatedById);
        Assert.Equal(Now, row.CreatedOnUtc);
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
        Assert.True((await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId)).IsBlacklisted);
        Assert.Equal(1, await db.BlacklistedCustomers.CountAsync(b => b.CustomerId == customerId));
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
    public async Task Unblock_clears_the_flag_and_appends_an_unblocked_history_row()
    {
        var customerId = SeedCustomer("Unblock");
        var adminId = Guid.NewGuid();
        await Block(customerId, "Chargeback", "blocked note");

        var id = await Unblock(customerId, "  appeal accepted  ", adminId);

        using var db = NewContext();
        Assert.False((await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId)).IsBlacklisted);

        var rows = await db.BlacklistedCustomers.AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, r => r.Action == BlacklistAction.Blocked);
        var unblocked = Assert.Single(rows, r => r.Action == BlacklistAction.Unblocked);
        Assert.Equal(id, unblocked.Id);
        Assert.Null(unblocked.Reason);
        Assert.Equal("appeal accepted", unblocked.InternalNotes);
        Assert.Equal(adminId, unblocked.CreatedById);
    }

    [Fact]
    public async Task Unblocking_a_customer_who_is_not_blacklisted_is_a_conflict()
    {
        var customerId = SeedCustomer("NeverBlocked");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Unblock(customerId));
        Assert.Equal(UnblockCustomerCommandHandler.NotBlacklistedMessage, ex.Message);

        using var db = NewContext();
        Assert.False((await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId)).IsBlacklisted);
        Assert.Equal(0, await db.BlacklistedCustomers.CountAsync(b => b.CustomerId == customerId));
    }

    [Fact]
    public async Task Unblocking_an_unknown_customer_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => Unblock(Guid.NewGuid()));

    [Fact]
    public async Task A_failed_history_insert_rolls_back_both_the_flag_and_the_history_row()
    {
        var customerId = SeedCustomer("Rollback");
        var constraint = "CK_BlacklistRollback_" + Guid.NewGuid().ToString("N");

        using (var db = NewContext())
        {
            // Inject a failing insert for one specific notes value (rolled back with the test transaction).
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE [BlacklistedCustomers] ADD CONSTRAINT [{constraint}] " +
                "CHECK ([InternalNotes] IS NULL OR [InternalNotes] <> N'__fail__')");
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => Block(customerId, "Chargeback", "__fail__"));

        using var check = NewContext();
        Assert.False((await check.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId)).IsBlacklisted);
        Assert.Equal(0, await check.BlacklistedCustomers.CountAsync(b => b.CustomerId == customerId));
    }

    [Fact]
    public async Task A_failed_unblock_history_insert_leaves_the_customer_blacklisted()
    {
        var customerId = SeedCustomer("UnblockRollback");
        await Block(customerId);
        var constraint = "CK_UnblockRollback_" + Guid.NewGuid().ToString("N");

        using (var db = NewContext())
        {
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE [BlacklistedCustomers] ADD CONSTRAINT [{constraint}] " +
                "CHECK ([InternalNotes] IS NULL OR [InternalNotes] <> N'__fail__')");
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => Unblock(customerId, "__fail__"));

        using var check = NewContext();
        Assert.True((await check.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId)).IsBlacklisted);
        Assert.Equal(1, await check.BlacklistedCustomers.CountAsync(b => b.CustomerId == customerId));
    }
}
