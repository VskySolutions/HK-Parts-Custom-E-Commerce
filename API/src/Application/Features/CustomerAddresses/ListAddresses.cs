using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CustomerAddresses;

/// <summary>Lists all of the authenticated customer's saved addresses (AC-CUS-002.2).</summary>
public record ListAddressesQuery : IRequest<List<AddressDto>>;

public class ListAddressesQueryHandler : IRequestHandler<ListAddressesQuery, List<AddressDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public ListAddressesQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<AddressDto>> Handle(ListAddressesQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .Where(c => c.UserId == _current.UserId.Value)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var entries = await _db.CustomerAddresses
            .AsNoTracking()
            .Include(m => m.Address)
            .Where(m => m.CustomerId == customerId)
            .OrderBy(m => m.AddressType)
            .ThenByDescending(m => m.IsDefault)
            .ThenBy(m => m.Address!.LastName)
            .ThenBy(m => m.Address!.FirstName)
            .ToListAsync(cancellationToken);

        return entries.Select(AddressDto.From).ToList();
    }
}
