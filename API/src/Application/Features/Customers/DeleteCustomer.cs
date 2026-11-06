using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Admin soft-deletes a customer. The <see cref="Customer"/> row is kept and flagged
/// <see cref="Customer.Deleted"/> (with <see cref="Customer.DeletedOnUtc"/>), which the global query filter then
/// hides from every customer query (admin list/search/counts, detail, cart, storefront profile). The login
/// (<see cref="User.IsActive"/> = false) is deactivated so sign-in and token refresh stop working.
/// Orders, addresses, store credit, reviews and every other related record are left untouched.
/// <para>The flags are set directly (rather than via <c>Remove</c>) so the DbContext also stamps
/// <c>UpdatedById/UpdatedOnUtc</c> with the acting admin; the audit trail records it as a <c>Delete</c>.</para>
/// </summary>
public record DeleteCustomerCommand(Guid CustomerId) : IRequest;

public class DeleteCustomerCommandValidator : AbstractValidator<DeleteCustomerCommand>
{
    public DeleteCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

public class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public DeleteCustomerCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        // Bypass the soft-delete filter so an already-deleted customer gets a clear 409 instead of a 404.
        var customer = await _db.Customers
            .IgnoreQueryFilters()
            .Include(c => c.User!)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        if (customer.Deleted)
            throw new ConflictException("This customer has already been deleted.");

        if (customer.User is null || customer.User.Deleted)
            throw new NotFoundException(nameof(User), request.CustomerId);

        // Staff accounts also carry a Customer profile; never let the customer screen deactivate them.
        var customerRole = nameof(RoleType.Customer);
        if (customer.User.UserRoles.Any(ur => ur.Role != null && ur.Role.Name != customerRole))
            throw new ConflictException("This account belongs to a staff user and cannot be deleted from Customers.");

        customer.Deleted = true;
        customer.DeletedOnUtc = _clock.UtcNow;
        customer.User.IsActive = false;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
