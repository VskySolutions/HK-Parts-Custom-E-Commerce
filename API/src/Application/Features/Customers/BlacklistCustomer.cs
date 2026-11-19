using System.Data;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Admin blacklists (blocks) a customer from the Customers list. In one transaction it flips
/// <see cref="Customer.IsBlacklisted"/> and appends a <see cref="BlacklistAction.Blocked"/> history row; the
/// customer record and all related data stay untouched, but the customer no longer appears in the admin
/// Customers list. Blocked-by and timestamps are stamped by the DbContext, and the admin audit trail records
/// the changes (same transaction). Only the customer id is trusted from the client; name/email are read from
/// the database.
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
                c.FirstName,
                c.LastName,
                Email = c.User != null ? c.User.Email : null,
                IsStaff = c.User != null && c.User.UserRoles.Any(ur => ur.Role != null && ur.Role.Name != nameof(RoleType.Customer)),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

            // Staff accounts also carry a Customer profile; the customer screen never acts on them (same rule as Delete).
            if (customer.IsStaff)
                throw new ConflictException("This account belongs to a staff user and cannot be blacklisted from Customers.");

            if (customer.IsBlacklisted)
                throw new ConflictException(AlreadyBlacklistedMessage);

        var entry = new BlacklistedCustomer
        {
            CustomerId = customer.Id,
            Reason = request.Reason!.Trim(),
            InternalNotes = string.IsNullOrWhiteSpace(request.InternalNotes) ? null : request.InternalNotes.Trim(),
        };
        _db.BlacklistedCustomers.Add(entry);

        // The Blocked Customers list (and the checkout block) work off CustomerBlacklistEntries: create the
        // customer's entry, or re-activate the one left from an earlier block, in the same transaction.
        var listEntry = await _db.CustomerBlacklistEntries
            .FirstOrDefaultAsync(e => e.CustomerId == customer.Id, cancellationToken);
        if (listEntry is null)
        {
            listEntry = new CustomerBlacklistEntry { CustomerId = customer.Id };
            _db.CustomerBlacklistEntries.Add(listEntry);
        }
        listEntry.FirstName = Clean(customer.FirstName);
        listEntry.LastName = Clean(customer.LastName);
        listEntry.Email = Clean(customer.Email)?.ToLowerInvariant();
        listEntry.Reason = entry.Reason;
        listEntry.Status = CustomerBlacklistStatus.Blocked;
        HKPartsEcommerce.Application.Features.Checkout.CustomerBlacklistHistory.Record(_db, listEntry, CustomerBlacklistAction.Blocked);

            await _db.SaveChangesAsync(ct);
            return entry.Id;
        }, cancellationToken);
}

/// <summary>
/// Removes a customer from the blacklist (not yet exposed in the UI). In one transaction it clears
/// <see cref="Customer.IsBlacklisted"/> and appends a <see cref="BlacklistAction.Unblocked"/> history row.
/// </summary>
public record UnblockCustomerCommand(Guid CustomerId, string? Notes) : IRequest<Guid>;

public class UnblockCustomerCommandValidator : AbstractValidator<UnblockCustomerCommand>
{
    public UnblockCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Notes)
            .Must(n => (n?.Trim().Length ?? 0) <= BlacklistedCustomer.InternalNotesMaxLength)
            .WithMessage($"Internal notes must be {BlacklistedCustomer.InternalNotesMaxLength} characters or fewer.");
    }
}

public class UnblockCustomerCommandHandler : IRequestHandler<UnblockCustomerCommand, Guid>
{
    public const string NotBlacklistedMessage = "This customer is not on the blacklist.";

    private readonly IApplicationDbContext _db;

    public UnblockCustomerCommandHandler(IApplicationDbContext db) => _db = db;

    public Task<Guid> Handle(UnblockCustomerCommand request, CancellationToken cancellationToken)
        => BlacklistTransactionRunner.RunAsync(_db, async ct =>
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

            if (!customer.IsBlacklisted)
                throw new ConflictException(NotBlacklistedMessage);

            customer.IsBlacklisted = false;
            var entry = new BlacklistedCustomer
            {
                CustomerId = customer.Id,
                Action = BlacklistAction.Unblocked,
                Reason = null,
                InternalNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            };
            _db.BlacklistedCustomers.Add(entry);

            await _db.SaveChangesAsync(ct);
            return entry.Id;
        }, cancellationToken);
}

/// <summary>
/// Runs a blacklist state change atomically. When the caller already owns a transaction (e.g. a wrapping test or
/// batch) a savepoint gives the same all-or-nothing behaviour; otherwise a Serializable transaction is opened —
/// so two concurrent blocks cannot both pass the IsBlacklisted = 0 check.
/// </summary>
internal static class BlacklistTransactionRunner
{
    public static async Task<T> RunAsync<T>(
        IApplicationDbContext db, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        string? savepoint = null;

        if (ownsTransaction)
        {
            transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        }
        else
        {
            savepoint = Guid.NewGuid().ToString("N");
            await db.Database.CurrentTransaction!.CreateSavepointAsync(savepoint, cancellationToken);
        }

        try
        {
            var result = await action(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (savepoint is not null)
                await db.Database.CurrentTransaction!.RollbackToSavepointAsync(savepoint, cancellationToken);
            else if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return entry.Id;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
