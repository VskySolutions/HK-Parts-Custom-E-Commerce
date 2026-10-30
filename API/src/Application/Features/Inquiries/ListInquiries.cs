using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Inquiries;

/// <summary>
/// Admin list of submitted inquiries (REQ-INQ-001), newest first. Server-side filters for the pipeline
/// status, the assigned store, whether a quote has been sent, and the submission date window — plus a
/// search across reference, contact name/email and company.
/// </summary>
public record ListInquiriesQuery(
    string? Status = null,
    Guid? StoreId = null,
    bool? Quoted = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<InquirySummaryDto>>;

public class ListInquiriesQueryHandler : IRequestHandler<ListInquiriesQuery, PaginatedList<InquirySummaryDto>>
{
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["referenceNumber"] = "OrderNumber",
        ["inquiryStatus"] = "InquiryStatus",
        ["submittedOnUtc"] = "PlacedOnUtc",
        ["estimatedValue"] = "TotalAmount",
    };

    private readonly IApplicationDbContext _db;

    public ListInquiriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<InquirySummaryDto>> Handle(
        ListInquiriesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Orders
            .AsNoTracking()
            .OnlyInquiries()
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .Include(o => o.AssignedStore)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<InquiryStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(o => o.InquiryStatus == status);
        }

        if (request.StoreId is Guid storeId)
            query = query.Where(o => o.AssignedStoreId == storeId);

        if (request.Quoted is bool quoted)
            query = quoted ? query.Where(o => o.QuotedOnUtc != null) : query.Where(o => o.QuotedOnUtc == null);

        if (request.FromUtc is DateTime from)
            query = query.Where(o => o.PlacedOnUtc >= from);

        if (request.ToUtc is DateTime to)
            query = query.Where(o => o.PlacedOnUtc <= to);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            // Contact fields live on the linked Address — Order.ContactName/ContactEmail are [NotMapped]
            // read-throughs and cannot be translated to SQL.
            query = query.Where(o => o.OrderNumber.Contains(term)
                || (o.ShippingAddress != null &&
                    ((o.ShippingAddress.FirstName != null && o.ShippingAddress.FirstName.Contains(term))
                     || (o.ShippingAddress.LastName != null && o.ShippingAddress.LastName.Contains(term))
                     || (o.ShippingAddress.Email != null && o.ShippingAddress.Email.Contains(term))))
                || (o.CompanyName != null && o.CompanyName.Contains(term)));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderByDescending(o => o.PlacedOnUtc));
        var page = await PaginatedList<Order>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(InquirySummaryDto.From).ToList();
        return new PaginatedList<InquirySummaryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
