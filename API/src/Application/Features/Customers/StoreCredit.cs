using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>One store-credit ledger entry (WO-48).</summary>
public class StoreCreditTransactionDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public StoreCreditTransactionType Type { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public Guid? RmaId { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static StoreCreditTransactionDto From(StoreCreditTransaction t) => new()
    {
        Id = t.Id,
        Amount = t.Amount,
        Type = t.Type,
        CurrencyCode = t.CurrencyCode,
        Reason = t.Reason,
        RmaId = t.RmaId,
        OrderId = t.OrderId,
        CreatedOnUtc = t.CreatedOnUtc,
    };
}

/// <summary>A customer's store-credit balance and its ledger (WO-48).</summary>
public class StoreCreditDto
{
    public Guid CustomerId { get; set; }
    public decimal Balance { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public List<StoreCreditTransactionDto> Transactions { get; set; } = new();
}

// ---- Read (shared) -----------------------------------------------------------

internal static class StoreCreditReader
{
    public static async Task<StoreCreditDto> LoadAsync(IApplicationDbContext db, Guid customerId, CancellationToken ct)
    {
        var rows = await db.StoreCreditTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedOnUtc)
            .ToListAsync(ct);

        return new StoreCreditDto
        {
            CustomerId = customerId,
            Balance = rows.Sum(t => t.Amount),
            CurrencyCode = rows.FirstOrDefault()?.CurrencyCode ?? "USD",
            Transactions = rows.Select(StoreCreditTransactionDto.From).ToList(),
        };
    }
}

// ---- Admin: get a customer's store credit ------------------------------------

/// <summary>Admin view of a customer's store-credit balance + ledger (WO-48).</summary>
public record GetCustomerStoreCreditQuery(Guid CustomerId) : IRequest<StoreCreditDto>;

public class GetCustomerStoreCreditQueryHandler : IRequestHandler<GetCustomerStoreCreditQuery, StoreCreditDto>
{
    private readonly IApplicationDbContext _db;
    public GetCustomerStoreCreditQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<StoreCreditDto> Handle(GetCustomerStoreCreditQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
            throw new NotFoundException(nameof(Customer), request.CustomerId);
        return await StoreCreditReader.LoadAsync(_db, request.CustomerId, cancellationToken);
    }
}

// ---- Customer: get my store credit -------------------------------------------

/// <summary>The signed-in customer's own store-credit balance + ledger (WO-48).</summary>
public record GetMyStoreCreditQuery : IRequest<StoreCreditDto>;

public class GetMyStoreCreditQueryHandler : IRequestHandler<GetMyStoreCreditQuery, StoreCreditDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetMyStoreCreditQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<StoreCreditDto> Handle(GetMyStoreCreditQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException("Authentication is required.");

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        return await StoreCreditReader.LoadAsync(_db, customer.Id, cancellationToken);
    }
}

// ---- Admin: manually issue / adjust store credit -----------------------------

/// <summary>Admin manual store-credit grant (a positive amount) with a reason (WO-48).</summary>
public record IssueStoreCreditCommand(Guid CustomerId, decimal Amount, string? Reason = null, string CurrencyCode = "USD")
    : IRequest<StoreCreditDto>;

public class IssueStoreCreditCommandValidator : AbstractValidator<IssueStoreCreditCommand>
{
    public IssueStoreCreditCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}

public class IssueStoreCreditCommandHandler : IRequestHandler<IssueStoreCreditCommand, StoreCreditDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IStoreCreditService _storeCredit;

    public IssueStoreCreditCommandHandler(IApplicationDbContext db, IStoreCreditService storeCredit)
    {
        _db = db;
        _storeCredit = storeCredit;
    }

    public async Task<StoreCreditDto> Handle(IssueStoreCreditCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
            throw new NotFoundException(nameof(Customer), request.CustomerId);

        await _storeCredit.IssueAsync(
            request.CustomerId, request.Amount, request.CurrencyCode,
            string.IsNullOrWhiteSpace(request.Reason) ? "Manual admin grant" : request.Reason,
            cancellationToken: cancellationToken);

        return await StoreCreditReader.LoadAsync(_db, request.CustomerId, cancellationToken);
    }
}
