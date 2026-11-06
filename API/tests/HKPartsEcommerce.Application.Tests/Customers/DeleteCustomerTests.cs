using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>
/// Admin customer soft delete (DELETE /api/admin/customers/{id}): the row is kept and flagged, the login is
/// deactivated, the customer disappears from the admin list, and related records survive. Real SQL Server test DB.
/// </summary>
public class DeleteCustomerTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private Guid SeedCustomer(string email, bool asStaff = false)
    {
        using var db = NewContext();
        Role FindOrCreate(string name) => db.Roles.FirstOrDefault(r => r.Name == name)
            ?? new Role { Name = name, NormalizedName = name.ToUpperInvariant(), IsSystemRole = true };

        var user = new User
        {
            Username = email,
            Email = email,
            PasswordHash = "x",
            EmailVerified = true,
            IsActive = true,
            Customer = new Customer { FirstName = "Del", LastName = "Me", PhoneNumber = "5550100" },
        };
        user.UserRoles.Add(new UserRole { Role = FindOrCreate(nameof(RoleType.Customer)), AssignedOnUtc = Now });
        if (asStaff)
            user.UserRoles.Add(new UserRole { Role = FindOrCreate(nameof(RoleType.TenantAdmin)), AssignedOnUtc = Now });
        db.Users.Add(user);
        db.SaveChanges();
        return user.Customer!.Id;
    }

    private DeleteCustomerCommandHandler Handler(HKPartsEcommerce.Infrastructure.Persistence.AppDbContext db)
        => new(db, new FixedClock(Now));

    [Fact]
    public async Task Delete_flags_customer_deactivates_login_and_keeps_the_row()
    {
        var id = SeedCustomer("del1@test.local");

        using (var db = NewContext())
            await Handler(db).Handle(new DeleteCustomerCommand(id), CancellationToken.None);

        using var check = NewContext();
        Assert.False(await check.Customers.AnyAsync(c => c.Id == id)); // hidden by the global filter
        var row = await check.Customers.IgnoreQueryFilters().Include(c => c.User).SingleAsync(c => c.Id == id);
        Assert.True(row.Deleted);
        Assert.Equal(Now, row.DeletedOnUtc);
        Assert.False(row.User!.IsActive);
        Assert.Equal("Del", row.FirstName);          // profile data preserved
        Assert.Equal("5550100", row.PhoneNumber);
    }

    [Fact]
    public async Task Deleted_customer_is_excluded_from_list_and_count()
    {
        var keep = SeedCustomer("keep@test.local");
        var gone = SeedCustomer("gone@test.local");

        using (var db = NewContext())
            await Handler(db).Handle(new DeleteCustomerCommand(gone), CancellationToken.None);

        using var list = NewContext();
        var result = await new ListCustomersQueryHandler(list).Handle(
            new ListCustomersQuery(Page: 1, PageSize: 50, Search: "@test.local"), CancellationToken.None);

        Assert.Contains(result.Items, c => c.Id == keep);
        Assert.DoesNotContain(result.Items, c => c.Id == gone);
        Assert.Equal(result.Items.Count, result.TotalCount);
    }

    [Fact]
    public async Task Delete_twice_throws_conflict()
    {
        var id = SeedCustomer("twice@test.local");
        using (var db = NewContext())
            await Handler(db).Handle(new DeleteCustomerCommand(id), CancellationToken.None);

        using var again = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => Handler(again).Handle(new DeleteCustomerCommand(id), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_unknown_customer_throws_not_found()
    {
        using var db = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => Handler(db).Handle(new DeleteCustomerCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_staff_account_is_refused_and_nothing_changes()
    {
        var id = SeedCustomer("staff-del@test.local", asStaff: true);

        using (var db = NewContext())
            await Assert.ThrowsAsync<ConflictException>(() => Handler(db).Handle(new DeleteCustomerCommand(id), CancellationToken.None));

        using var check = NewContext();
        var row = await check.Customers.Include(c => c.User).SingleAsync(c => c.Id == id);
        Assert.False(row.Deleted);
        Assert.True(row.User!.IsActive);
    }

    [Fact]
    public void Validator_rejects_empty_id()
    {
        Assert.False(new DeleteCustomerCommandValidator().Validate(new DeleteCustomerCommand(Guid.Empty)).IsValid);
    }
}
