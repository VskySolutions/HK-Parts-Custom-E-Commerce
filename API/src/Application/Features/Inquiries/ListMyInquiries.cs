using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Inquiries;

/// <summary>
/// The authenticated customer's own inquiries (REQ-INQ-001), newest first. Scoped strictly to the
/// caller's customer profile. Guests can submit inquiries but have no account to list them in — their
/// acknowledgement email carries the reference instead.
/// </summary>
public record ListMyInquiriesQuery(int Page = 1, int PageSize = 20) : IRequest<PaginatedList<InquirySummaryDto>>;

public class ListMyInquiriesQueryHandler : IRequestHandler<ListMyInquiriesQuery, PaginatedList<InquirySummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public ListMyInquiriesQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PaginatedList<InquirySummaryDto>> Handle(
        ListMyInquiriesQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var ordered = _db.Orders
            .AsNoTracking()
            .OnlyInquiries()
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .Include(o => o.AssignedStore)
            .AsSplitQuery()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedOnUtc);

        var page = await PaginatedList<Order>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(InquirySummaryDto.From).ToList();
        return new PaginatedList<InquirySummaryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
