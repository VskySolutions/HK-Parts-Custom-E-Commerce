using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HKPartsEcommerce.Application.Features.Roles;

public record ListRolesQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool SortDescending = false) : IRequest<PaginatedList<RoleDto>>;

public class ListRolesQueryHandler : IRequestHandler<ListRolesQuery, PaginatedList<RoleDto>>
{

    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name"
    };

    private readonly IApplicationDbContext _db;

    public ListRolesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<RoleDto>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Role> query = _db.Roles.AsNoTracking().OrderBy(r => r.Name);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(f => f.Name.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(m => m.Name));

        var page = await PaginatedList<Role>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(RoleDto.From).ToList();
        return new PaginatedList<RoleDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
