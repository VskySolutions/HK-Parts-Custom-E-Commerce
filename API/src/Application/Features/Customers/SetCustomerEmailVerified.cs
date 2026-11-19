using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Admin marks a customer's email as verified, or back to unverified. The flag lives on the underlying
/// <see cref="User"/>; an unverified customer cannot sign in (CustomerLogin / unified Login), so this is how
/// an admin lets in a customer whose verification email was lost or expired.
/// </summary>
public record SetCustomerEmailVerifiedCommand(Guid CustomerId, bool EmailVerified) : IRequest<CustomerDetailDto>;

public class SetCustomerEmailVerifiedCommandHandler : IRequestHandler<SetCustomerEmailVerifiedCommand, CustomerDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public SetCustomerEmailVerifiedCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<CustomerDetailDto> Handle(SetCustomerEmailVerifiedCommand request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        if (customer.User is null)
            throw new NotFoundException(nameof(User), request.CustomerId);

        customer.User.EmailVerified = request.EmailVerified;
        await _db.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(new GetCustomerDetailQuery(request.CustomerId), cancellationToken);
    }
}
