using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CustomerGroups;

/// <summary>
/// Assigns a customer to exactly one pricing group, replacing any previous group (AC-CUS-003.2).
/// A null <paramref name="CustomerGroupId"/> clears the group — the customer reverts to base pricing.
/// </summary>
public record AssignCustomerGroupCommand(Guid CustomerId, Guid? CustomerGroupId) : IRequest<CustomerGroupBriefDto?>;

public class AssignCustomerGroupCommandHandler : IRequestHandler<AssignCustomerGroupCommand, CustomerGroupBriefDto?>
{
    private readonly IApplicationDbContext _db;
    public AssignCustomerGroupCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerGroupBriefDto?> Handle(AssignCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        if (request.CustomerGroupId is not Guid groupId)
        {
            customer.CustomerGroupId = null;
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var group = await _db.CustomerGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerGroup), groupId);

        // Replacement is just an overwrite — there is only ever one group per customer.
        customer.CustomerGroupId = group.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return CustomerGroupBriefDto.From(group);
    }
}

/// <summary>The customer's assigned pricing group, or null when they are on base pricing.</summary>
public record GetCustomerGroupForCustomerQuery(Guid CustomerId) : IRequest<CustomerGroupBriefDto?>;

public class GetCustomerGroupForCustomerQueryHandler : IRequestHandler<GetCustomerGroupForCustomerQuery, CustomerGroupBriefDto?>
{
    private readonly IApplicationDbContext _db;
    public GetCustomerGroupForCustomerQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerGroupBriefDto?> Handle(GetCustomerGroupForCustomerQuery request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.AsNoTracking()
            .Include(c => c.CustomerGroup)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        return customer.CustomerGroup is null ? null : CustomerGroupBriefDto.From(customer.CustomerGroup);
    }
}
