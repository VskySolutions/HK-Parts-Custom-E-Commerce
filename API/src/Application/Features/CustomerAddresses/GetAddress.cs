using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CustomerAddresses;

/// <summary>Gets one of the authenticated customer's addresses by id (ownership enforced).</summary>
public record GetAddressQuery(Guid Id) : IRequest<AddressDto>;

public class GetAddressQueryHandler : IRequestHandler<GetAddressQuery, AddressDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetAddressQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<AddressDto> Handle(GetAddressQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .Where(c => c.UserId == _current.UserId.Value)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        // Scoped to the current customer: a foreign or missing id is indistinguishable (both 404).
        var entry = await _db.CustomerAddresses
            .AsNoTracking()
            .Include(m => m.Address)
            .FirstOrDefaultAsync(m => m.Id == request.Id && m.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerAddress), request.Id);

        return AddressDto.From(entry);
    }
}
