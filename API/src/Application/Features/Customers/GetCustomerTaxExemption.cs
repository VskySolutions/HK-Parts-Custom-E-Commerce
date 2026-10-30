using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>Returns a customer's current tax-exemption configuration (REQ-TAX-003).</summary>
public record GetCustomerTaxExemptionQuery(Guid CustomerId) : IRequest<CustomerTaxExemptionDto>;

public class GetCustomerTaxExemptionQueryHandler : IRequestHandler<GetCustomerTaxExemptionQuery, CustomerTaxExemptionDto>
{
    private readonly IApplicationDbContext _db;

    public GetCustomerTaxExemptionQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerTaxExemptionDto> Handle(GetCustomerTaxExemptionQuery request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        return CustomerTaxExemptionDto.From(customer);
    }
}
