using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerAddresses;

/// <summary>
/// Returns the authenticated customer's addresses grouped by type (shipping/billing) for checkout
/// address selection (AC-CUS-002.2). Default addresses are listed first within each group.
/// </summary>
public record GetAddressBookQuery : IRequest<AddressBookDto>;

public class GetAddressBookQueryHandler : IRequestHandler<GetAddressBookQuery, AddressBookDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetAddressBookQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<AddressBookDto> Handle(GetAddressBookQuery request, CancellationToken cancellationToken)
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
            .OrderByDescending(m => m.IsDefault)
            .ThenBy(m => m.Address!.LastName)
            .ThenBy(m => m.Address!.FirstName)
            .ToListAsync(cancellationToken);

        return new AddressBookDto
        {
            Shipping = entries.Where(m => m.AddressType == AddressType.Shipping).Select(AddressDto.From).ToList(),
            Billing = entries.Where(m => m.AddressType == AddressType.Billing).Select(AddressDto.From).ToList(),
        };
    }
}
