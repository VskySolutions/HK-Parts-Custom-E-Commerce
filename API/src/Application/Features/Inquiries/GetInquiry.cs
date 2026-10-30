using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Inquiries;

/// <summary>A single inquiry with its contact details and requested items (REQ-INQ-001).</summary>
public record GetInquiryQuery(Guid Id) : IRequest<InquiryDto>;

public class GetInquiryQueryHandler : IRequestHandler<GetInquiryQuery, InquiryDto>
{
    private readonly IApplicationDbContext _db;

    public GetInquiryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<InquiryDto> Handle(GetInquiryQuery request, CancellationToken cancellationToken)
    {
        // Loadable after conversion too (IsInquiry is false by then) so the detail page still resolves from
        // a bookmarked link and can point at the resulting order.
        var inquiry = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .Include(o => o.AssignedStore)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.InquiryStatus != null, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.Id);

        return InquiryDto.From(inquiry);
    }
}
