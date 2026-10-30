using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>A customer row for the admin customer-management list (WO-117).</summary>
public class CustomerListItemDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>The customer's pricing group, or null for base pricing (AC-CUS-003.2).</summary>
    public Guid? CustomerGroupId { get; set; }
    public string? CustomerGroupName { get; set; }

    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
}

/// <summary>
/// Lists customers (newest first) for the admin grid (WO-117), optionally filtered by name/email search,
/// pricing group, email-verified, account-active, tax-exempt state and registration-date range.
/// </summary>
public record ListCustomersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? EmailVerified = null,
    bool? IsActive = null,
    bool? IsTaxExempt = null,
    Guid? CustomerGroupId = null,
    bool? HasCustomerGroup = null,
    DateTime? RegisteredFromUtc = null,
    DateTime? RegisteredToUtc = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<CustomerListItemDto>>;

public class ListCustomersQueryHandler : IRequestHandler<ListCustomersQuery, PaginatedList<CustomerListItemDto>>
{
    // Grid column name -> entity property path (customer fields are read through the User nav). Anything
    // else falls back to CreatedOnUtc desc. orderCount/totalSpent are aggregates computed after paging and
    // are deliberately not sortable.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "FirstName",
        ["email"] = "User.Email",
        ["phoneNumber"] = "PhoneNumber",
        ["createdOnUtc"] = "CreatedOnUtc",
        ["emailVerified"] = "User.EmailVerified",
        ["isActive"] = "User.IsActive",
        ["customerGroupName"] = "CustomerGroup.Name",
    };

    private readonly IApplicationDbContext _db;

    public ListCustomersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CustomerListItemDto>> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        // Only genuine customers: a customer is a User carrying the Customer system role (assigned on
        // registration and mapped for pre-existing accounts). Staff carry admin roles instead — and they
        // also get a Customer profile at creation, so filtering by the Customer role is what excludes them.
        var customerRole = nameof(RoleType.Customer);
        var query = _db.Customers.AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.CustomerGroup)
            .Where(c => c.User != null && c.User.UserRoles.Any(ur => ur.Role!.Name == customerRole))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c =>
                c.FirstName.Contains(term)
                || c.LastName.Contains(term)
                || (c.User != null && c.User.Email.Contains(term)));
        }

        if (request.EmailVerified.HasValue)
            query = query.Where(c => c.User != null && c.User.EmailVerified == request.EmailVerified.Value);

        if (request.IsActive.HasValue)
            query = query.Where(c => c.User != null && c.User.IsActive == request.IsActive.Value);

        if (request.IsTaxExempt.HasValue)
            query = query.Where(c => c.IsTaxExempt == request.IsTaxExempt.Value);

        if (request.CustomerGroupId.HasValue)
            query = query.Where(c => c.CustomerGroupId == request.CustomerGroupId.Value);
        else if (request.HasCustomerGroup.HasValue)
            query = request.HasCustomerGroup.Value
                ? query.Where(c => c.CustomerGroupId != null)
                : query.Where(c => c.CustomerGroupId == null);

        if (request.RegisteredFromUtc.HasValue)
            query = query.Where(c => c.CreatedOnUtc >= request.RegisteredFromUtc.Value);

        if (request.RegisteredToUtc.HasValue)
            query = query.Where(c => c.CreatedOnUtc <= request.RegisteredToUtc.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap);
        var page = await PaginatedList<Customer>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);

        // Order aggregates for just this page, in one grouped round-trip (never per row).
        var ids = page.Items.Select(c => c.Id).ToList();
        var aggregates = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId != null && ids.Contains(o.CustomerId.Value))
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries()
            .GroupBy(o => o.CustomerId!.Value)
            .Select(g => new
            {
                CustomerId = g.Key,
                OrderCount = g.Count(),
                // Lifetime value counts only captured money, matching the detail view.
                TotalSpent = g.Where(o => o.PaymentStatus == PaymentStatus.Captured
                                          || o.PaymentStatus == PaymentStatus.PartiallyRefunded)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0m,
            })
            .ToDictionaryAsync(x => x.CustomerId, cancellationToken);

        var items = page.Items.Select(c => new CustomerListItemDto
        {
            Id = c.Id,
            FirstName = c.FirstName,
            LastName = c.LastName,
            Email = c.User?.Email ?? string.Empty,
            PhoneNumber = c.PhoneNumber,
            EmailVerified = c.User?.EmailVerified ?? false,
            IsActive = c.User?.IsActive ?? false,
            CreatedOnUtc = c.CreatedOnUtc,
            CustomerGroupId = c.CustomerGroupId,
            CustomerGroupName = c.CustomerGroup?.Name,
            OrderCount = aggregates.TryGetValue(c.Id, out var a) ? a.OrderCount : 0,
            TotalSpent = aggregates.TryGetValue(c.Id, out var b) ? b.TotalSpent : 0m,
        }).ToList();

        return new PaginatedList<CustomerListItemDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
