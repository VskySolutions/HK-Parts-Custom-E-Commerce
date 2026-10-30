using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Accounts;

/// <summary>
/// Verifies the admin customer list only returns genuine customers (a User carrying the Customer system
/// role) and never staff/admin accounts — which carry admin roles and also have a Customer profile.
/// </summary>
public class CustomerListFilterTests : CatalogTestBase
{
    private void SeedAccount(string email, string firstName, bool asAdmin)
    {
        using var db = NewContext();
        var user = new User
        {
            Username = email,
            Email = email,
            PasswordHash = "hashed:whatever",
            EmailVerified = true,
            IsActive = true,
            Customer = new Customer { FirstName = firstName, LastName = "Test" },
        };
        if (asAdmin)
        {
            var role = new Role { Name = "TenantAdmin-" + Guid.NewGuid().ToString("n"), NormalizedName = "TA", IsSystemRole = true };
            user.UserRoles.Add(new UserRole { Role = role, AssignedOnUtc = DateTime.UtcNow });
        }
        else
        {
            // A customer now carries the seeded Customer system role. Find-or-create within the test
            // transaction so multiple customers reuse one role row (NormalizedName is unique).
            var customerRole = db.Roles.FirstOrDefault(r => r.Name == nameof(RoleType.Customer))
                ?? new Role
                {
                    Name = nameof(RoleType.Customer),
                    NormalizedName = nameof(RoleType.Customer).ToUpperInvariant(),
                    IsSystemRole = true,
                };
            user.UserRoles.Add(new UserRole { Role = customerRole, AssignedOnUtc = DateTime.UtcNow });
        }
        db.Users.Add(user);
        db.SaveChanges();
    }

    [Fact]
    public async Task List_excludes_admin_accounts_and_includes_customers()
    {
        SeedAccount("shopper@test.com", "Shopper", asAdmin: false);
        SeedAccount("staff@test.com", "Staff", asAdmin: true);

        using var db = NewContext();
        var handler = new ListCustomersQueryHandler(db);
        var result = await handler.Handle(new ListCustomersQuery(Page: 1, PageSize: 50), CancellationToken.None);

        Assert.Contains(result.Items, c => c.Email == "shopper@test.com");
        Assert.DoesNotContain(result.Items, c => c.Email == "staff@test.com");
    }

    [Fact]
    public async Task List_search_still_never_returns_admins()
    {
        SeedAccount("admin.jane@test.com", "Jane", asAdmin: true);
        SeedAccount("jane.shopper@test.com", "Jane", asAdmin: false);

        using var db = NewContext();
        var handler = new ListCustomersQueryHandler(db);
        var result = await handler.Handle(new ListCustomersQuery(Page: 1, PageSize: 50, Search: "Jane"), CancellationToken.None);

        Assert.Contains(result.Items, c => c.Email == "jane.shopper@test.com");
        Assert.DoesNotContain(result.Items, c => c.Email == "admin.jane@test.com");
    }
}
