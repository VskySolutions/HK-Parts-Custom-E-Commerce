using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>The admin Customers list filters (search + Advanced drawer), shared by the list and bulk actions.</summary>
public record CustomerListFilter(
    string? Search = null,
    bool? EmailVerified = null,
    bool? IsActive = null,
    bool? IsTaxExempt = null,
    Guid? CustomerGroupId = null,
    bool? HasCustomerGroup = null,
    DateTime? RegisteredFromUtc = null,
    DateTime? RegisteredToUtc = null);

/// <summary>
/// One definition of "the customers the admin list shows", so a bulk action over "all matching customers"
/// targets exactly the rows the admin was looking at.
/// </summary>
public static class CustomerListFilters
{
    /// <summary>
    /// Genuine customers only (a User carrying the Customer system role — staff also get a Customer profile at
    /// creation, so the role is what excludes them), minus blacklisted customers (the current-state
    /// <see cref="Customer.IsBlacklisted"/> flag).
    /// </summary>
    public static IQueryable<Customer> ListedCustomers(IApplicationDbContext db)
    {
        var customerRole = nameof(RoleType.Customer);
        return db.Customers.AsNoTracking()
            .Where(c => c.User != null && c.User.UserRoles.Any(ur => ur.Role!.Name == customerRole))
            .Where(c => !c.IsBlacklisted);
    }

    public static IQueryable<Customer> Apply(IQueryable<Customer> query, CustomerListFilter? f)
    {
        if (f is null)
            return query;

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            query = query.Where(c =>
                c.FirstName.Contains(term)
                || c.LastName.Contains(term)
                || (c.User != null && c.User.Email.Contains(term)));
        }

        if (f.EmailVerified.HasValue)
            query = query.Where(c => c.User != null && c.User.EmailVerified == f.EmailVerified.Value);

        if (f.IsActive.HasValue)
            query = query.Where(c => c.User != null && c.User.IsActive == f.IsActive.Value);

        if (f.IsTaxExempt.HasValue)
            query = query.Where(c => c.IsTaxExempt == f.IsTaxExempt.Value);

        if (f.CustomerGroupId.HasValue)
            query = query.Where(c => c.CustomerGroupId == f.CustomerGroupId.Value);
        else if (f.HasCustomerGroup.HasValue)
            query = f.HasCustomerGroup.Value
                ? query.Where(c => c.CustomerGroupId != null)
                : query.Where(c => c.CustomerGroupId == null);

        if (f.RegisteredFromUtc.HasValue)
            query = query.Where(c => c.CreatedOnUtc >= f.RegisteredFromUtc.Value);

        if (f.RegisteredToUtc.HasValue)
            query = query.Where(c => c.CreatedOnUtc <= f.RegisteredToUtc.Value);

        return query;
    }
}
