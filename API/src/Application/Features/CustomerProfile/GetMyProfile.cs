using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CustomerProfile;

/// <summary>Returns the authenticated customer's own profile (AC-CUS-002.1).</summary>
public record GetMyProfileQuery : IRequest<CustomerProfileDto>;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, CustomerProfileDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetMyProfileQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CustomerProfileDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == _current.UserId.Value, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        return CustomerProfileDto.From(customer, customer.User!);
    }
}
