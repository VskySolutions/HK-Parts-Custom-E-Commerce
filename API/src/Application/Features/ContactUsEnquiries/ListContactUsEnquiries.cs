using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ContactUsEnquiries;

/// <summary>Returns every product custom field (a small, admin-curated list) by display order then name.</summary>
public record ListContactUsEnquiriesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<ContactUsDto>>;

public class ListContactUsEnquiriesQueryHandler : IRequestHandler<ListContactUsEnquiriesQuery, PaginatedList<ContactUsDto>>
{
    // Grid column name -> entity property path. Anything else falls back to the natural order below.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "Email",
    };

    private readonly IApplicationDbContext _db;

    public ListContactUsEnquiriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ContactUsDto>> Handle(ListContactUsEnquiriesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ContactUs> query = _db.ContactUsEnquiries.AsNoTracking().Where(x => !x.Deleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.FirstName.Contains(term) || (c.LastName != null && c.LastName.Contains(term)) || (c.Email != null && c.Email.Contains(term)) || (c.PhoneNumber != null && c.PhoneNumber.Contains(term)));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(c => c.CreatedOnUtc));

        // Project in SQL so ProductCount is a COUNT sub-query rather than loading every item row to count it.
        var projected = ordered.Select(c => new ContactUsDto
        {
            Id = c.Id,
            FirstName = c.FirstName,
            LastName = c.LastName,
            FullName = c.FirstName +" "+ c.LastName,
            Email = c.Email,
            PhoneNumber = c.PhoneNumber,
            Comments = c.Comments,
            CreatedOnUtc = c.CreatedOnUtc,
        });

        return await PaginatedList<ContactUsDto>.CreateAsync(projected, request.Page, request.PageSize, cancellationToken);
    }
}
