using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Exempts a customer from the restricted-state shipping rule (SR-92), or removes the exemption. An exempt,
/// signed-in buyer may receive state-restricted products in a restricted state.
/// </summary>
public record SetCustomerShippingRestrictionExemptCommand(Guid CustomerId, bool IsExempt) : IRequest<CustomerDetailDto>;

public class SetCustomerShippingRestrictionExemptCommandHandler
    : IRequestHandler<SetCustomerShippingRestrictionExemptCommand, CustomerDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public SetCustomerShippingRestrictionExemptCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<CustomerDetailDto> Handle(SetCustomerShippingRestrictionExemptCommand request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        customer.IsShippingRestrictionExempt = request.IsExempt;
        await _db.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(new GetCustomerDetailQuery(request.CustomerId), cancellationToken);
    }
}
