using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.AdminUsers;

/// <summary>Returns a page of users (newest first by default), optionally filtered by an email/username search term, active state and/or email-verified state, and sortable by an allow-listed column.</summary>
public record ListUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null,
    bool? EmailVerified = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<AdminUserDto>>;

public class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, PaginatedList<AdminUserDto>>
{
    // Column name (from the grid) -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "Email",
        ["fullName"] = "Customer.FirstName",
        ["isActive"] = "IsActive",
        ["emailVerified"] = "EmailVerified",
    };

    private readonly IApplicationDbContext _db;

    public ListUsersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<AdminUserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<User> query = _db.Users
            .AsNoTracking()
            .Include(u => u.Customer)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role);

        // Admin/staff accounts only — exclude storefront customers. Customers carry the Customer system
        // role; staff carry at least one other (non-Customer) role.
        var customerRole = nameof(RoleType.Customer);
        query = query.Where(u => u.UserRoles.Any(ur => ur.Role!.Name != customerRole));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.Username.Contains(term));
        }

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        if (request.EmailVerified.HasValue)
            query = query.Where(u => u.EmailVerified == request.EmailVerified.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(u => u.Email));

        var page = await PaginatedList<User>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(AdminUserDto.From).ToList();
        return new PaginatedList<AdminUserDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
