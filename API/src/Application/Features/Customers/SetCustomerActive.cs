using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Activates or deactivates a customer's account (WO-117). The flag lives on the underlying
/// <see cref="User"/>, so deactivating blocks sign-in without touching the customer's profile or history.
/// </summary>
public record SetCustomerActiveCommand(Guid CustomerId, bool IsActive) : IRequest<CustomerDetailDto>;

public class SetCustomerActiveCommandHandler : IRequestHandler<SetCustomerActiveCommand, CustomerDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public SetCustomerActiveCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<CustomerDetailDto> Handle(SetCustomerActiveCommand request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        if (customer.User is null)
            throw new NotFoundException(nameof(User), request.CustomerId);

        customer.User.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(new GetCustomerDetailQuery(request.CustomerId), cancellationToken);
    }
}
