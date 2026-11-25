using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Checkout;

public class CustomerBlacklistEntryDto
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Guest checkout A-31: "Guest", "Customer" or "Manual"; null for entries made before sources were kept.</summary>
    public string? Source { get; set; }
    public int HitCount { get; set; }
    public DateTime? LastHitUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static CustomerBlacklistEntryDto From(CustomerBlacklistEntry e, int hitCount = 0, DateTime? lastHit = null) => new()
    {
        Id = e.Id,
        FirstName = e.FirstName,
        LastName = e.LastName,
        Email = e.Email,
        Reason = e.Reason,
        Status = e.Status.ToString(),
        Source = e.Source,
        HitCount = hitCount,
        LastHitUtc = lastHit,
        CreatedOnUtc = e.CreatedOnUtc,
        UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public class CustomerBlacklistHitDto
{
    public Guid Id { get; set; }
    public Guid BlacklistEntryId { get; set; }
    public Guid? CartId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? IpAddress { get; set; }
    public string MatchedOn { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }

    public static CustomerBlacklistHitDto From(CustomerBlacklistHit h) => new()
    {
        Id = h.Id,
        BlacklistEntryId = h.BlacklistEntryId,
        CartId = h.CartId,
        FirstName = h.FirstName,
        LastName = h.LastName,
        Email = h.Email,
        IpAddress = h.IpAddress,
        MatchedOn = h.MatchedOn,
        AtUtc = h.AtUtc,
    };
}

/// <summary>
/// Pure matching rule for the checkout blacklist (PY-10 / PY-15). Names match on billing first + last name,
/// both required, case-insensitive and trimmed; email matches case-insensitive. An entry with no email never
/// matches on email, and one without a full name never matches on name.
/// </summary>
public static class CustomerBlacklistMatcher
{
    public record MatchResult(CustomerBlacklistEntry Entry, string MatchedOn);

    public static MatchResult? Match(
        IEnumerable<CustomerBlacklistEntry> entries, string? firstName, string? lastName, string? email, BlacklistMatchMode mode)
    {
        var f = Norm(firstName);
        var l = Norm(lastName);
        var e = Norm(email);

        foreach (var entry in entries.Where(x => x.Status == CustomerBlacklistStatus.Blocked))
        {
            var ef = Norm(entry.FirstName);
            var el = Norm(entry.LastName);
            var ee = Norm(entry.Email);
            var nameHit = f is not null && l is not null && ef is not null && el is not null && f == ef && l == el;
            var emailHit = e is not null && ee is not null && e == ee;

            var hit = mode switch
            {
                BlacklistMatchMode.EmailOnly => emailHit,
                BlacklistMatchMode.NameAndEmail => nameHit && emailHit,
                _ => nameHit || emailHit,
            };
            if (hit)
                return new MatchResult(entry, nameHit && emailHit ? "Name+Email" : emailHit ? "Email" : "Name");
        }

        return null;
    }

    private static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToLowerInvariant();
}

public record ListCustomerBlacklistQuery(string? Search = null, CustomerBlacklistStatus? Status = null, int Page = 1, int PageSize = 25)
    : IRequest<PaginatedList<CustomerBlacklistEntryDto>>;

public class ListCustomerBlacklistQueryHandler : IRequestHandler<ListCustomerBlacklistQuery, PaginatedList<CustomerBlacklistEntryDto>>
{
    private readonly IApplicationDbContext _db;

    public ListCustomerBlacklistQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CustomerBlacklistEntryDto>> Handle(ListCustomerBlacklistQuery request, CancellationToken ct)
    {
        var query = _db.CustomerBlacklistEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x => (x.Email != null && x.Email.Contains(s))
                                     || (x.FirstName != null && x.FirstName.Contains(s))
                                     || (x.LastName != null && x.LastName.Contains(s))
                                     || (x.Reason != null && x.Reason.Contains(s)));
        }
        if (request.Status is CustomerBlacklistStatus status)
            query = query.Where(x => x.Status == status);

        var page = await PaginatedList<CustomerBlacklistEntry>.CreateAsync(
            query.OrderByDescending(x => x.CreatedOnUtc), request.Page, Math.Clamp(request.PageSize, 1, 200), ct);

        var ids = page.Items.Select(x => x.Id).ToList();

        // An entry is reused when a customer is unblocked and blocked again, so its hits span every block
        // period. Count only the refused attempts since the current block began (the latest "Blocked" event);
        // entries with no such event (created before the history existed) count all their hits.
        var blockedSince = await _db.CustomerBlacklistEvents.AsNoTracking()
            .Where(e => e.BlacklistEntryId != null && ids.Contains(e.BlacklistEntryId.Value)
                        && e.Action == CustomerBlacklistAction.Blocked)
            .GroupBy(e => e.BlacklistEntryId!.Value)
            .Select(g => new { Id = g.Key, Since = g.Max(e => e.AtUtc) })
            .ToDictionaryAsync(x => x.Id, x => x.Since, ct);

        var hitRows = await _db.CustomerBlacklistHits.AsNoTracking()
            .Where(h => ids.Contains(h.BlacklistEntryId))
            .Select(h => new { h.BlacklistEntryId, h.AtUtc })
            .ToListAsync(ct);
        var hits = hitRows
            .Where(h => !blockedSince.TryGetValue(h.BlacklistEntryId, out var since) || h.AtUtc >= since)
            .GroupBy(h => h.BlacklistEntryId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Last: g.Max(h => h.AtUtc)));

        var items = page.Items
            .Select(x => hits.TryGetValue(x.Id, out var h)
                ? CustomerBlacklistEntryDto.From(x, h.Count, h.Last)
                : CustomerBlacklistEntryDto.From(x))
            .ToList();
        return new PaginatedList<CustomerBlacklistEntryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

public record SaveCustomerBlacklistEntryCommand(
    Guid? Id,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Reason,
    CustomerBlacklistStatus Status = CustomerBlacklistStatus.Blocked,
    /// <summary>Guest checkout A-31: the screen the block came from ("Guest", "Customer"); new entries default to "Manual".</summary>
    string? Source = null) : IRequest<CustomerBlacklistEntryDto>;

public class SaveCustomerBlacklistEntryCommandValidator : AbstractValidator<SaveCustomerBlacklistEntryCommand>
{
    public SaveCustomerBlacklistEntryCommandValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Reason).MaximumLength(1000);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email)
                       || (!string.IsNullOrWhiteSpace(x.FirstName) && !string.IsNullOrWhiteSpace(x.LastName)))
            .WithName("Email")
            .WithMessage("Enter an email address, or both a first and last name.");
    }
}

public class SaveCustomerBlacklistEntryCommandHandler : IRequestHandler<SaveCustomerBlacklistEntryCommand, CustomerBlacklistEntryDto>
{
    private readonly IApplicationDbContext _db;

    public SaveCustomerBlacklistEntryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerBlacklistEntryDto> Handle(SaveCustomerBlacklistEntryCommand r, CancellationToken ct)
    {
        CustomerBlacklistEntry entry;
        CustomerBlacklistStatus? previous = null;
        if (r.Id is Guid id)
        {
            entry = await _db.CustomerBlacklistEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException(nameof(CustomerBlacklistEntry), id);
            previous = entry.Status;
        }
        else
        {
            entry = new CustomerBlacklistEntry { Source = BlacklistSources.Normalize(r.Source) };
            _db.CustomerBlacklistEntries.Add(entry);
        }

        entry.FirstName = Clean(r.FirstName);
        entry.LastName = Clean(r.LastName);
        entry.Email = Clean(r.Email)?.ToLowerInvariant();
        entry.Reason = Clean(r.Reason);
        entry.Status = r.Status;
        if (entry.CustomerId is Guid customerId)
            await CustomerBlacklistSync.ApplyAsync(_db, customerId, entry.Status == CustomerBlacklistStatus.Blocked, entry.Reason, ct);

        // History: a new blocked entry, or a status change on an existing one.
        if (previous != entry.Status && (previous is not null || entry.Status == CustomerBlacklistStatus.Blocked))
            CustomerBlacklistHistory.Record(_db, entry,
                entry.Status == CustomerBlacklistStatus.Blocked ? CustomerBlacklistAction.Blocked : CustomerBlacklistAction.Unblocked);
        await _db.SaveChangesAsync(ct);
        return CustomerBlacklistEntryDto.From(entry);
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

/// <summary>
/// Keeps a customer-linked blacklist entry and the customer's <see cref="Customer.IsBlacklisted"/> flag (which hides
/// them from the Customers list) in step. When the flag changes, a <see cref="BlacklistedCustomer"/> history row
/// (Blocked / Unblocked) is appended. Changes are staged only; the caller saves.
/// </summary>
internal static class CustomerBlacklistSync
{
    public static async Task ApplyAsync(IApplicationDbContext db, Guid customerId, bool blocked, string? reason, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null || customer.IsBlacklisted == blocked)
            return;

        customer.IsBlacklisted = blocked;
        db.BlacklistedCustomers.Add(new BlacklistedCustomer
        {
            CustomerId = customerId,
            Action = blocked ? BlacklistAction.Blocked : BlacklistAction.Unblocked,
            Reason = blocked ? (string.IsNullOrWhiteSpace(reason) ? "Blocked" : reason.Trim()) : null,
        });
    }
}

public record DeleteCustomerBlacklistEntryCommand(Guid Id) : IRequest;

public class DeleteCustomerBlacklistEntryCommandHandler : IRequestHandler<DeleteCustomerBlacklistEntryCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCustomerBlacklistEntryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCustomerBlacklistEntryCommand request, CancellationToken ct)
    {
        var entry = await _db.CustomerBlacklistEntries.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                    ?? throw new NotFoundException(nameof(CustomerBlacklistEntry), request.Id);
        if (entry.CustomerId is Guid customerId)
            await CustomerBlacklistSync.ApplyAsync(_db, customerId, blocked: false, reason: null, ct);
        if (entry.Status == CustomerBlacklistStatus.Blocked)
            CustomerBlacklistHistory.Record(_db, entry, CustomerBlacklistAction.Unblocked);
        _db.CustomerBlacklistEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Refused placement attempts (PY-15), newest first; optionally for one entry.</summary>
public record ListCustomerBlacklistHitsQuery(Guid? EntryId = null, int Page = 1, int PageSize = 25)
    : IRequest<PaginatedList<CustomerBlacklistHitDto>>;

public class ListCustomerBlacklistHitsQueryHandler : IRequestHandler<ListCustomerBlacklistHitsQuery, PaginatedList<CustomerBlacklistHitDto>>
{
    private readonly IApplicationDbContext _db;

    public ListCustomerBlacklistHitsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CustomerBlacklistHitDto>> Handle(ListCustomerBlacklistHitsQuery request, CancellationToken ct)
    {
        var query = _db.CustomerBlacklistHits.AsNoTracking();
        if (request.EntryId is Guid id)
            query = query.Where(h => h.BlacklistEntryId == id);
        var page = await PaginatedList<CustomerBlacklistHit>.CreateAsync(
            query.OrderByDescending(h => h.AtUtc), request.Page, Math.Clamp(request.PageSize, 1, 200), ct);
        return new PaginatedList<CustomerBlacklistHitDto>(
            page.Items.Select(CustomerBlacklistHitDto.From).ToList(), page.TotalCount, page.PageNumber, page.PageSize);
    }
}

/// <summary>Where a blacklist entry was created (guest checkout A-31).</summary>
public static class BlacklistSources
{
    public const string Guest = "Guest";
    public const string Customer = "Customer";
    public const string Manual = "Manual";

    public static string Normalize(string? source) =>
        string.Equals(source, Guest, StringComparison.OrdinalIgnoreCase) ? Guest
        : string.Equals(source, Customer, StringComparison.OrdinalIgnoreCase) ? Customer
        : Manual;
}
