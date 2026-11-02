using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.InventoryScanner;

// ---- Dashboard -------------------------------------------------------------------------------------

/// <summary>
/// Scanner dashboard (PHP InventoryController::dashboard): products updated today, the last update time
/// and the most recent updates. "Today" is the business day in the tenant's display timezone (Tenant
/// branding → DisplayTimeZone, the zone every admin timestamp is shown in), not the server's clock.
/// </summary>
public record GetInventoryScannerDashboardQuery(int Take = 10) : IRequest<InventoryScannerDashboardDto>;

public class GetInventoryScannerDashboardQueryHandler : IRequestHandler<GetInventoryScannerDashboardQuery, InventoryScannerDashboardDto>
{
    private readonly IApplicationDbContext _db;

    public GetInventoryScannerDashboardQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<InventoryScannerDashboardDto> Handle(GetInventoryScannerDashboardQuery request, CancellationToken cancellationToken)
    {
        var tzId = await _db.TenantBrandings.AsNoTracking()
            .Select(b => b.DisplayTimeZone).FirstOrDefaultAsync(cancellationToken);
        var tz = ResolveTimeZone(tzId);
        var (startUtc, endUtc) = BusinessDayUtc(DateTime.UtcNow, tz);

        var today = _db.InventoryAuditLogs.AsNoTracking()
            .Where(l => l.CreatedOnUtc >= startUtc && l.CreatedOnUtc < endUtc);
        var take = Math.Clamp(request.Take, 1, 50);

        return new InventoryScannerDashboardDto
        {
            ProductsUpdatedToday = await today.Select(l => l.ProductId).Distinct().CountAsync(cancellationToken),
            UpdatesToday = await today.CountAsync(cancellationToken),
            LastUpdateUtc = await _db.InventoryAuditLogs.AsNoTracking()
                .OrderByDescending(l => l.CreatedOnUtc).Select(l => (DateTime?)l.CreatedOnUtc).FirstOrDefaultAsync(cancellationToken),
            BusinessTimeZone = tz.Id,
            RecentUpdates = await InventoryScannerLookup.ProjectLogsAsync(_db,
                _db.InventoryAuditLogs.OrderByDescending(l => l.CreatedOnUtc).ThenByDescending(l => l.Id).Take(take),
                cancellationToken),
        };
    }

    /// <summary>The tenant zone, or UTC when unset / unknown to this host.</summary>
    internal static TimeZoneInfo ResolveTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz) ? tz : TimeZoneInfo.Utc;

    /// <summary>
    /// [start, end) in UTC of the local calendar day containing <paramref name="nowUtc"/> in
    /// <paramref name="tz"/>. Handles DST days (23 or 25 hours) and zones whose midnight is skipped.
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) BusinessDayUtc(DateTime nowUtc, TimeZoneInfo tz)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);
        var startLocal = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        return (ToUtc(startLocal, tz), ToUtc(startLocal.AddDays(1), tz));
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo tz)
    {
        // A midnight that falls in a spring-forward gap doesn't exist; the day starts at the first valid minute.
        while (tz.IsInvalidTime(local)) local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }
}

// ---- Lookup ----------------------------------------------------------------------------------------

/// <summary>
/// Resolve a scanned barcode / typed SKU to a product or variant (PHP getProduct / productDetail).
/// Exact match on SKU, then GTIN, product then variant. 400 (validation) when empty or longer than 100
/// characters. No match / several matches are returned in the result, not thrown — the controller turns
/// them into 404 / 409 (with up to 10 <c>candidates</c>).
/// </summary>
public record LookupScannedProductQuery(string Code) : IRequest<ScannerLookupResult>;

public class LookupScannedProductQueryValidator : AbstractValidator<LookupScannedProductQuery>
{
    public LookupScannedProductQueryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Please enter a SKU number.")
            .Must(c => c is null || c.Trim().Length <= InventoryScannerLookup.MaxCodeLength)
            .WithMessage($"The code must be {InventoryScannerLookup.MaxCodeLength} characters or fewer.");
    }
}

public class LookupScannedProductQueryHandler : IRequestHandler<LookupScannedProductQuery, ScannerLookupResult>
{
    private readonly IApplicationDbContext _db;

    public LookupScannedProductQueryHandler(IApplicationDbContext db) => _db = db;

    public Task<ScannerLookupResult> Handle(LookupScannedProductQuery request, CancellationToken cancellationToken)
        => InventoryScannerLookup.ResolveAsync(_db, request.Code, cancellationToken);
}

// ---- Product by id (detail screen) -----------------------------------------------------------------

/// <summary>
/// The scanner's product screen, addressed by id (from the lookup result) rather than by the raw code, so
/// '/', '+', '%' in Code 39 labels never reach the URL path and an ambiguous code is resolved only once.
/// Null (not thrown) when the product / variant doesn't exist; the controller answers 404.
/// </summary>
public record GetScannerProductQuery(Guid ProductId, Guid? ProductVariantId = null) : IRequest<ScannedProductDto?>;

public class GetScannerProductQueryHandler : IRequestHandler<GetScannerProductQuery, ScannedProductDto?>
{
    private readonly IApplicationDbContext _db;

    public GetScannerProductQueryHandler(IApplicationDbContext db) => _db = db;

    public Task<ScannedProductDto?> Handle(GetScannerProductQuery request, CancellationToken cancellationToken)
        => InventoryScannerLookup.BuildAsync(_db, request.ProductId, request.ProductVariantId, cancellationToken);
}

// ---- Single log (success screen) -------------------------------------------------------------------

/// <summary>
/// One scanner update with the product's current state (PHP successPage). Read by the audit id the update
/// returned — never "latest row for this product", which would race with a colleague's scan.
/// </summary>
public record GetInventoryScanLogQuery(Guid Id) : IRequest<InventoryScanLogDto>;

public class GetInventoryScanLogQueryHandler : IRequestHandler<GetInventoryScanLogQuery, InventoryScanLogDto>
{
    private readonly IApplicationDbContext _db;

    public GetInventoryScanLogQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<InventoryScanLogDto> Handle(GetInventoryScanLogQuery request, CancellationToken cancellationToken)
        => await InventoryScannerLookup.GetLogAsync(_db, request.Id, cancellationToken)
           ?? throw new NotFoundException(nameof(InventoryAuditLog), request.Id);
}

// ---- Per-product history ---------------------------------------------------------------------------

/// <summary>
/// Paged scanner audit rows for one product, newest first. With <paramref name="ProductVariantId"/> only
/// that variant's rows; without it every row for the product (including its variants'). PageSize ≤ 100.
/// </summary>
public record GetInventoryScanHistoryQuery(Guid ProductId, Guid? ProductVariantId = null, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedList<InventoryScanLogDto>>;

public class GetInventoryScanHistoryQueryHandler : IRequestHandler<GetInventoryScanHistoryQuery, PaginatedList<InventoryScanLogDto>>
{
    private readonly IApplicationDbContext _db;

    public GetInventoryScanHistoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<InventoryScanLogDto>> Handle(GetInventoryScanHistoryQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);

        var query = _db.InventoryAuditLogs.Where(l => l.ProductId == request.ProductId);
        if (request.ProductVariantId is Guid vid)
            query = query.Where(l => l.ProductVariantId == vid);

        var count = await query.CountAsync(cancellationToken);
        var items = await InventoryScannerLookup.ProjectLogsAsync(_db,
            query.OrderByDescending(l => l.CreatedOnUtc).ThenByDescending(l => l.Id).Skip((page - 1) * size).Take(size),
            cancellationToken);
        return new PaginatedList<InventoryScanLogDto>(items, count, page, size);
    }
}
