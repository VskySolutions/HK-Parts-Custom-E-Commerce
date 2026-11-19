using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Admin blocks several customers at once from the Customers list, with one reason / internal note for all.
/// Targets either the ticked <see cref="CustomerIds"/>, or — with <see cref="AllMatching"/> — every customer the
/// list currently shows for <see cref="Filter"/> (all pages). Each customer gets exactly what the single
/// <see cref="BlacklistCustomerCommand"/> writes: a <see cref="BlacklistedCustomer"/> row plus its Blocked
/// Customers entry (re-activated if left from an earlier block) and a history event, all in one transaction.
/// Customers already blocked, staff accounts and unknown ids are skipped, not failed.
/// </summary>
public record BulkBlacklistCustomersCommand(
    List<Guid>? CustomerIds,
    bool AllMatching,
    CustomerListFilter? Filter,
    string? Reason,
    string? InternalNotes) : IRequest<BulkBlacklistCustomersResult>;

public class BulkBlacklistCustomersResult
{
    public int Blocked { get; set; }
    public int Skipped { get; set; }
}

public class BulkBlacklistCustomersCommandValidator : AbstractValidator<BulkBlacklistCustomersCommand>
{
    public const int MaxIds = BulkBlacklistCustomersCommandHandler.MaxCustomers;

    public BulkBlacklistCustomersCommandValidator()
    {
        RuleFor(x => x.CustomerIds)
            .Must(ids => ids is { Count: > 0 }).WithMessage("Select at least one customer.")
            .When(x => !x.AllMatching);
        RuleFor(x => x.CustomerIds!.Count)
            .LessThanOrEqualTo(MaxIds).WithMessage($"At most {MaxIds} customers can be blocked at once.")
            .When(x => !x.AllMatching && x.CustomerIds is not null);
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason for blocking is required.")
            .Must(r => (r?.Trim().Length ?? 0) <= BlacklistedCustomer.ReasonMaxLength)
            .WithMessage($"Reason for blocking must be {BlacklistedCustomer.ReasonMaxLength} characters or fewer.");
        RuleFor(x => x.InternalNotes)
            .Must(n => (n?.Trim().Length ?? 0) <= BlacklistedCustomer.InternalNotesMaxLength)
            .WithMessage($"Internal notes must be {BlacklistedCustomer.InternalNotesMaxLength} characters or fewer.");
    }
}

public class BulkBlacklistCustomersCommandHandler : IRequestHandler<BulkBlacklistCustomersCommand, BulkBlacklistCustomersResult>
{
    /// <summary>Upper bound for one request (keeps the single transaction and the IN-lists reasonable).</summary>
    public const int MaxCustomers = 5000;

    private readonly IApplicationDbContext _db;

    public BulkBlacklistCustomersCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BulkBlacklistCustomersResult> Handle(BulkBlacklistCustomersCommand request, CancellationToken ct)
    {
        // The list's own definition: genuine customers, not already blocked. Staff never appear there, and a
        // user holding any non-Customer role is excluded too (same rule as the single block).
        var customerRole = nameof(RoleType.Customer);
        var query = CustomerListFilters.ListedCustomers(_db)
            .Where(c => !c.User!.UserRoles.Any(ur => ur.Role != null && ur.Role.Name != customerRole));

        List<Guid>? requested = null;
        if (request.AllMatching)
        {
            query = CustomerListFilters.Apply(query, request.Filter);
        }
        else
        {
            requested = request.CustomerIds!.Distinct().ToList();
            query = query.Where(c => requested.Contains(c.Id));
        }

        var targets = await query
            .OrderBy(c => c.CreatedOnUtc)
            .Take(MaxCustomers + 1)
            .Select(c => new { c.Id, c.FirstName, c.LastName, Email = c.User!.Email })
            .ToListAsync(ct);
        if (targets.Count > MaxCustomers)
            throw new Common.Exceptions.ConflictException(
                $"More than {MaxCustomers} customers match. Narrow the filters and block them in smaller batches.");

        var result = new BulkBlacklistCustomersResult
        {
            Blocked = targets.Count,
            Skipped = requested is null ? 0 : requested.Count - targets.Count,
        };
        if (targets.Count == 0)
            return result;

        var reason = request.Reason!.Trim();
        var notes = string.IsNullOrWhiteSpace(request.InternalNotes) ? null : request.InternalNotes.Trim();
        var ids = targets.Select(t => (Guid?)t.Id).ToList();
        var existing = await _db.CustomerBlacklistEntries
            .Where(e => e.CustomerId != null && ids.Contains(e.CustomerId))
            .ToListAsync(ct);
        var byCustomer = existing
            .GroupBy(e => e.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var t in targets)
        {
            _db.BlacklistedCustomers.Add(new BlacklistedCustomer
            {
                CustomerId = t.Id,
                Reason = reason,
                InternalNotes = notes,
            });

            if (!byCustomer.TryGetValue(t.Id, out var entry))
            {
                entry = new CustomerBlacklistEntry { CustomerId = t.Id };
                _db.CustomerBlacklistEntries.Add(entry);
            }
            entry.FirstName = Clean(t.FirstName);
            entry.LastName = Clean(t.LastName);
            entry.Email = Clean(t.Email)?.ToLowerInvariant();
            entry.Reason = reason;
            entry.Status = CustomerBlacklistStatus.Blocked;
            CustomerBlacklistHistory.Record(_db, entry, CustomerBlacklistAction.Blocked);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent block of one of these customers (filtered unique index): nothing was
            // saved, so ask the admin to retry — the retry skips whoever is now blocked.
            throw new Common.Exceptions.ConflictException(
                "Some of these customers were blocked by someone else in the meantime. Refresh the list and try again.");
        }

        return result;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
