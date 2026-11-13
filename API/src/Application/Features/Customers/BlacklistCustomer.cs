using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Admin blacklists (blocks) a customer from the Customers list. Writes one <see cref="BlacklistedCustomer"/> row;
/// the customer record and all related data stay untouched, but the customer no longer appears in the admin
/// Customers list. Blocked-by and timestamps are stamped by the DbContext, and the admin audit trail records the
/// create (same transaction). Only the customer id is trusted from the client; name/email are never accepted.
/// </summary>
public record BlacklistCustomerCommand(Guid CustomerId, string? Reason, string? InternalNotes) : IRequest<Guid>;

public class BlacklistCustomerCommandValidator : AbstractValidator<BlacklistCustomerCommand>
{
    public BlacklistCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason for blocking is required.")
            .Must(r => (r?.Trim().Length ?? 0) <= BlacklistedCustomer.ReasonMaxLength)
            .WithMessage($"Reason for blocking must be {BlacklistedCustomer.ReasonMaxLength} characters or fewer.");
        RuleFor(x => x.InternalNotes)
            .Must(n => (n?.Trim().Length ?? 0) <= BlacklistedCustomer.InternalNotesMaxLength)
            .WithMessage($"Internal notes must be {BlacklistedCustomer.InternalNotesMaxLength} characters or fewer.");
    }
}

public class BlacklistCustomerCommandHandler : IRequestHandler<BlacklistCustomerCommand, Guid>
{
    public const string AlreadyBlacklistedMessage = "This customer is already on the blacklist.";

    private readonly IApplicationDbContext _db;

    public BlacklistCustomerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(BlacklistCustomerCommand request, CancellationToken cancellationToken)
    {
        // Soft-deleted customers are hidden by the global filter → 404, like every other customer endpoint.
        var customer = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == request.CustomerId)
            .Select(c => new
            {
                c.Id,
                IsStaff = c.User != null && c.User.UserRoles.Any(ur => ur.Role != null && ur.Role.Name != nameof(RoleType.Customer)),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        // Staff accounts also carry a Customer profile; the customer screen never acts on them (same rule as Delete).
        if (customer.IsStaff)
            throw new ConflictException("This account belongs to a staff user and cannot be blacklisted from Customers.");

        if (await _db.BlacklistedCustomers.AnyAsync(b => b.CustomerId == customer.Id, cancellationToken))
            throw new ConflictException(AlreadyBlacklistedMessage);

        var entry = new BlacklistedCustomer
        {
            CustomerId = customer.Id,
            Reason = request.Reason!.Trim(),
            InternalNotes = string.IsNullOrWhiteSpace(request.InternalNotes) ? null : request.InternalNotes.Trim(),
        };
        _db.BlacklistedCustomers.Add(entry);

        try
        {
            // Single SaveChanges = one transaction (the blacklist row + its audit-trail row).
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent block: the filtered unique index rejected the second active row.
            if (await _db.BlacklistedCustomers.AsNoTracking().AnyAsync(b => b.CustomerId == customer.Id, cancellationToken))
                throw new ConflictException(AlreadyBlacklistedMessage);
            throw;
        }

        return entry.Id;
    }
}
