using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.TaxExemptionRequests;

// ---- Customer portal ---------------------------------------------------------

/// <summary>The signed-in customer's exemption status and latest request (AC-TAX-003.3).</summary>
public record GetMyTaxExemptionQuery : IRequest<MyTaxExemptionDto>;

public class GetMyTaxExemptionQueryHandler : IRequestHandler<GetMyTaxExemptionQuery, MyTaxExemptionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetMyTaxExemptionQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<MyTaxExemptionDto> Handle(GetMyTaxExemptionQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException("Authentication is required.");

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var latest = await _db.TaxExemptionRequests
            .AsNoTracking()
            .Include(r => r.Documents)
            .Where(r => r.CustomerId == customer.Id)
            .OrderByDescending(r => r.SubmittedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = latest is null ? null : TaxExemptionRequestDto.From(latest);
        if (dto is not null)
            await HydrateDocumentsAsync(_db, new[] { dto }, cancellationToken);

        return new MyTaxExemptionDto
        {
            Status = latest is null ? "NotSubmitted" : latest.Status.ToString(),
            IsTaxExempt = customer.IsTaxExempt,
            // A new request is allowed whenever nothing is awaiting review (AC-TAX-003.6).
            CanSubmit = latest is null || latest.Status != TaxExemptionRequestStatus.PendingReview,
            LatestRequest = dto,
        };
    }

    /// <summary>Fills in each document's Media file name + URL (Media is referenced by id, not by FK).</summary>
    internal static async Task HydrateDocumentsAsync(
        IApplicationDbContext db, IReadOnlyCollection<TaxExemptionRequestDto> requests, CancellationToken ct)
    {
        var mediaIds = requests.SelectMany(r => r.Documents).Select(d => d.MediaId).Distinct().ToList();
        if (mediaIds.Count == 0)
            return;

        var media = await db.Media
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.OriginalFileName, m.Url })
            .ToDictionaryAsync(m => m.Id, ct);

        foreach (var document in requests.SelectMany(r => r.Documents))
        {
            if (!media.TryGetValue(document.MediaId, out var m))
                continue;
            document.FileName = m.OriginalFileName;
            document.Url = m.Url;
        }
    }
}

// ---- Admin queue -------------------------------------------------------------

/// <summary>Lists tax exemption requests for the admin review queue (AC-TAX-003.4).</summary>
public record ListTaxExemptionRequestsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<TaxExemptionRequestDto>>;

public class ListTaxExemptionRequestsQueryHandler
    : IRequestHandler<ListTaxExemptionRequestsQuery, PaginatedList<TaxExemptionRequestDto>>
{
    // Column name (from the grid) -> entity property path. Anything else falls back to SubmittedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = "Status",
            ["submittedOnUtc"] = "SubmittedOnUtc",
            ["reviewedOnUtc"] = "ReviewedOnUtc",
            ["certificateNumber"] = "CertificateNumber",
        };

    private readonly IApplicationDbContext _db;
    public ListTaxExemptionRequestsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<TaxExemptionRequestDto>> Handle(
        ListTaxExemptionRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.TaxExemptionRequests.AsNoTracking().Include(r => r.Documents).AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<TaxExemptionRequestStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            // Customer is referenced by id only, so search joins through the Customers set.
            var matching = _db.Customers.AsNoTracking()
                .Where(c => c.FirstName.Contains(term) || c.LastName.Contains(term)
                            || (c.User != null && c.User.Email.Contains(term)))
                .Select(c => c.Id);
            query = query.Where(r => matching.Contains(r.CustomerId)
                                     || (r.CertificateNumber != null && r.CertificateNumber.Contains(term))
                                     || (r.VatId != null && r.VatId.Contains(term)));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap, defaultProperty: "SubmittedOnUtc");
        var page = await PaginatedList<TaxExemptionRequest>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);

        var items = page.Items.Select(TaxExemptionRequestDto.From).ToList();
        await HydrateCustomersAsync(_db, items, cancellationToken);
        await GetMyTaxExemptionQueryHandler.HydrateDocumentsAsync(_db, items, cancellationToken);

        return new PaginatedList<TaxExemptionRequestDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    /// <summary>Fills in customer name/email for the queue (CustomerId is a plain id, not a nav).</summary>
    internal static async Task HydrateCustomersAsync(
        IApplicationDbContext db, IReadOnlyCollection<TaxExemptionRequestDto> requests, CancellationToken ct)
    {
        var ids = requests.Select(r => r.CustomerId).Distinct().ToList();
        if (ids.Count == 0)
            return;

        var customers = await db.Customers
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.FirstName, c.LastName, Email = c.User!.Email })
            .ToDictionaryAsync(c => c.Id, ct);

        foreach (var request in requests)
        {
            if (!customers.TryGetValue(request.CustomerId, out var c))
                continue;
            request.CustomerName = $"{c.FirstName} {c.LastName}".Trim();
            request.CustomerEmail = c.Email;
        }
    }
}

/// <summary>One request with its documents, for the admin review drawer (AC-TAX-003.4).</summary>
public record GetTaxExemptionRequestQuery(Guid Id) : IRequest<TaxExemptionRequestDto>;

public class GetTaxExemptionRequestQueryHandler : IRequestHandler<GetTaxExemptionRequestQuery, TaxExemptionRequestDto>
{
    private readonly IApplicationDbContext _db;
    public GetTaxExemptionRequestQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<TaxExemptionRequestDto> Handle(GetTaxExemptionRequestQuery request, CancellationToken cancellationToken)
    {
        var found = await _db.TaxExemptionRequests
            .AsNoTracking()
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TaxExemptionRequest), request.Id);

        var dto = TaxExemptionRequestDto.From(found);
        await ListTaxExemptionRequestsQueryHandler.HydrateCustomersAsync(_db, new[] { dto }, cancellationToken);
        await GetMyTaxExemptionQueryHandler.HydrateDocumentsAsync(_db, new[] { dto }, cancellationToken);
        return dto;
    }
}
