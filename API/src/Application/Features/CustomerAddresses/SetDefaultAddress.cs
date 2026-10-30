using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CustomerAddresses;

/// <summary>
/// Makes one of the authenticated customer's saved addresses the default for its type (AC-CUS-002.3).
/// Unlike <see cref="UpdateAddressCommand"/> this flips only the default flag, so it neither requires nor
/// re-validates the full address — the storefront/account "Set as default" action can promote an address
/// even if it is missing an optional field the full-update validator would reject.
/// </summary>
public record SetDefaultAddressCommand(Guid Id) : IRequest<AddressDto>;

public class SetDefaultAddressCommandHandler : IRequestHandler<SetDefaultAddressCommand, AddressDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public SetDefaultAddressCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<AddressDto> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .Where(c => c.UserId == _current.UserId.Value)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var mapping = await _db.CustomerAddresses
            .Include(m => m.Address)
            .FirstOrDefaultAsync(m => m.Id == request.Id && m.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerAddress), request.Id);

        if (!mapping.IsDefault)
        {
            // Clear any existing default of the same type first (separate save) so the filtered unique index
            // (one default per customer+type) is never transiently violated, then promote this one.
            var currentDefaults = await _db.CustomerAddresses
                .Where(m => m.CustomerId == customerId && m.AddressType == mapping.AddressType
                            && m.Id != mapping.Id && m.IsDefault)
                .ToListAsync(cancellationToken);

            if (currentDefaults.Count > 0)
            {
                foreach (var other in currentDefaults)
                    other.IsDefault = false;
                await _db.SaveChangesAsync(cancellationToken);
            }

            mapping.IsDefault = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return AddressDto.From(mapping);
    }
}
