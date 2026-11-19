using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

// ---- Admin: list + get -------------------------------------------------------

/// <summary>
/// The admin returns queue (RT-31): filter by status, resolution, reason, FFL flag, date range and a search over
/// RMA number / order number / customer email.
/// </summary>
public record ListRmasQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Resolution = null,
    string? SortBy = null,
    bool SortDescending = false,
    Guid? ReasonId = null,
    bool? RequiresFfl = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? OrderId = null,
    bool OpenOnly = false) : IRequest<PaginatedList<RmaSummaryDto>>;

public class ListRmasQueryHandler : IRequestHandler<ListRmasQuery, PaginatedList<RmaSummaryDto>>
{
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["rmaNumber"] = "RmaNumber",
        ["requestedOnUtc"] = "RequestedOnUtc",
        ["resolution"] = "Resolution",
        ["status"] = "Status",
        ["totalRefund"] = "TotalRefund",
        ["statusChangedOnUtc"] = "StatusChangedOnUtc",
    };

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ListRmasQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PaginatedList<RmaSummaryDto>> Handle(ListRmasQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Domain.Entities.Rma> query = _db.Rmas.AsNoTracking().Include(r => r.Lines);

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<RmaStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(r => r.Status == status);

        if (request.OpenOnly)
            query = query.Where(r => r.Status != RmaStatus.Rejected && r.Status != RmaStatus.Cancelled
                                     && r.Status != RmaStatus.Expired && r.Status != RmaStatus.Closed);

        if (!string.IsNullOrWhiteSpace(request.Resolution)
            && Enum.TryParse<RmaResolution>(request.Resolution, ignoreCase: true, out var resolution))
            query = query.Where(r => r.Resolution == resolution || (r.Resolution == RmaResolution.None && r.RequestedResolution == resolution));

        if (request.ReasonId is Guid reasonId)
            query = query.Where(r => r.Lines.Any(l => l.ReasonId == reasonId));
        if (request.RequiresFfl is bool ffl)
            query = query.Where(r => r.RequiresFfl == ffl);
        if (request.FromUtc is DateTime from)
            query = query.Where(r => r.RequestedOnUtc >= from);
        if (request.ToUtc is DateTime to)
            query = query.Where(r => r.RequestedOnUtc < to);
        if (request.OrderId is Guid orderId)
            query = query.Where(r => r.OrderId == orderId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var orderIds = _db.Orders.IgnoreQueryFilters()
                .Where(o => o.OrderNumber.Contains(term) || (o.ShippingAddress != null && o.ShippingAddress.Email.Contains(term)))
                .Select(o => o.Id);
            query = query.Where(r => r.RmaNumber.Contains(term) || orderIds.Contains(r.OrderId));
        }

        var ordered = string.IsNullOrWhiteSpace(request.SortBy)
            ? query.OrderByDescending(r => r.RequestedOnUtc)
            : query.ApplySort(request.SortBy, request.SortDescending, SortMap);
        var page = await PaginatedList<Domain.Entities.Rma>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);

        var ids = page.Items.Select(r => r.OrderId).Distinct().ToList();
        var orders = await _db.Orders.IgnoreQueryFilters().AsNoTracking().Include(o => o.ShippingAddress)
            .Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);

        var now = _clock.UtcNow;
        return new PaginatedList<RmaSummaryDto>(
            page.Items.Select(r => RmaSummaryDto.From(r, orders.GetValueOrDefault(r.OrderId), now)).ToList(),
            page.TotalCount, page.PageNumber, page.PageSize);
    }
}

/// <summary>Count of returns waiting for review, for the admin menu badge (RT-30).</summary>
public record CountPendingRmasQuery : IRequest<int>;

public class CountPendingRmasQueryHandler : IRequestHandler<CountPendingRmasQuery, int>
{
    private readonly IApplicationDbContext _db;
    public CountPendingRmasQueryHandler(IApplicationDbContext db) => _db = db;

    public Task<int> Handle(CountPendingRmasQuery request, CancellationToken cancellationToken)
        => _db.Rmas.CountAsync(r => r.Status == RmaStatus.Requested, cancellationToken);
}

/// <summary>Gets one return with history, messages and the refund breakdown (admin).</summary>
public record GetRmaQuery(Guid Id) : IRequest<RmaDto>;

public class GetRmaQueryHandler : IRequestHandler<GetRmaQuery, RmaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public GetRmaQueryHandler(IApplicationDbContext db, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _settings = settings;
        _clock = clock;
    }

    public async Task<RmaDto> Handle(GetRmaQuery request, CancellationToken cancellationToken)
    {
        var rma = await RmaSupport.LoadAsync(_db, request.Id, cancellationToken);
        return await RmaSupport.ToDtoAsync(_db, _settings, rma, null, _clock.UtcNow, cancellationToken);
    }
}

/// <summary>Staff view of what an order can still return (the "Create return" dialog). Policy rules are reported, not enforced.</summary>
public record GetOrderReturnabilityQuery(Guid OrderId) : IRequest<ReturnEligibilityDto>;

public class GetOrderReturnabilityQueryHandler : IRequestHandler<GetOrderReturnabilityQuery, ReturnEligibilityDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public GetOrderReturnabilityQueryHandler(IApplicationDbContext db, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _settings = settings;
        _clock = clock;
    }

    public async Task<ReturnEligibilityDto> Handle(GetOrderReturnabilityQuery request, CancellationToken cancellationToken)
    {
        var order = await RmaSupport.LoadOrderAsync(_db, request.OrderId, cancellationToken, tracked: false);
        var settings = await ReturnSettingsStore.LoadAsync(_settings, cancellationToken);
        return await ReturnEligibility.EvaluateAsync(_db, order, settings, _clock.UtcNow, forStaff: true, null, cancellationToken);
    }
}

// ---- Buyer: my returns -------------------------------------------------------

/// <summary>Lists the current customer's own returns, newest first (RT-22).</summary>
public record ListMyRmasQuery(int Page = 1, int PageSize = 20) : IRequest<PaginatedList<RmaSummaryDto>>;

public class ListMyRmasQueryHandler : IRequestHandler<ListMyRmasQuery, PaginatedList<RmaSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;

    public ListMyRmasQueryHandler(IApplicationDbContext db, ICurrentUserService current, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _clock = clock;
    }

    public async Task<PaginatedList<RmaSummaryDto>> Handle(ListMyRmasQuery request, CancellationToken cancellationToken)
    {
        var customer = await RmaSupport.CurrentCustomerAsync(_db, _current, cancellationToken);

        var query = _db.Rmas.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.CustomerId == customer.Id)
            .OrderByDescending(r => r.RequestedOnUtc);

        var page = await PaginatedList<Domain.Entities.Rma>.CreateAsync(query, request.Page, request.PageSize, cancellationToken);
        var ids = page.Items.Select(r => r.OrderId).Distinct().ToList();
        var orders = await _db.Orders.IgnoreQueryFilters().AsNoTracking().Include(o => o.ShippingAddress)
            .Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);
        var now = _clock.UtcNow;

        return new PaginatedList<RmaSummaryDto>(
            page.Items.Select(r =>
            {
                var s = RmaSummaryDto.From(r, orders.GetValueOrDefault(r.OrderId), now);
                s.LastRefundError = null;
                s.CustomerEmail = null;
                return s;
            }).ToList(),
            page.TotalCount, page.PageNumber, page.PageSize);
    }
}

/// <summary>One of the current customer's returns (RT-95: a foreign RMA is simply not found).</summary>
public record GetMyRmaQuery(Guid Id) : IRequest<RmaDto>;

public class GetMyRmaQueryHandler : IRequestHandler<GetMyRmaQuery, RmaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public GetMyRmaQueryHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _settings = settings;
        _clock = clock;
    }

    public async Task<RmaDto> Handle(GetMyRmaQuery request, CancellationToken cancellationToken)
    {
        var customer = await RmaSupport.CurrentCustomerAsync(_db, _current, cancellationToken);
        var rma = await RmaSupport.LoadAsync(_db, request.Id, cancellationToken);
        if (rma.CustomerId != customer.Id)
            throw new NotFoundException(nameof(Domain.Entities.Rma), request.Id);
        return (await RmaSupport.ToDtoAsync(_db, _settings, rma, null, _clock.UtcNow, cancellationToken)).ForCustomer();
    }
}

/// <summary>What the customer can return from one of their orders (drives the "Return items" button and form).</summary>
public record GetMyOrderReturnabilityQuery(Guid OrderId) : IRequest<ReturnEligibilityDto>;

public class GetMyOrderReturnabilityQueryHandler : IRequestHandler<GetMyOrderReturnabilityQuery, ReturnEligibilityDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public GetMyOrderReturnabilityQueryHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _settings = settings;
        _clock = clock;
    }

    public async Task<ReturnEligibilityDto> Handle(GetMyOrderReturnabilityQuery request, CancellationToken cancellationToken)
    {
        var customer = await RmaSupport.CurrentCustomerAsync(_db, _current, cancellationToken);
        var order = await _db.Orders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.CustomerId == customer.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);
        var settings = await ReturnSettingsStore.LoadAsync(_settings, cancellationToken);
        return await ReturnEligibility.EvaluateAsync(_db, order, settings, _clock.UtcNow, forStaff: false, null, cancellationToken);
    }
}
