using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.PriceRules;

// ---- List -----------------------------------------------------------------------------------------------

/// <summary>A page of price rules (default: name ascending), filtered by name, status and store.</summary>
public record ListPriceRulesQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    bool? IsActive = null,
    Guid? StoreId = null,
    string? Sort = null,
    string? Dir = null) : IRequest<PaginatedList<PriceRuleListItemDto>>;

public class ListPriceRulesHandler : IRequestHandler<ListPriceRulesQuery, PaginatedList<PriceRuleListItemDto>>
{
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = nameof(PriceRule.Name),
        ["isActive"] = nameof(PriceRule.IsActive),
        ["priority"] = nameof(PriceRule.Priority),
        ["discountAmount"] = nameof(PriceRule.DiscountAmount),
        ["startDate"] = nameof(PriceRule.StartDate),
        ["endDate"] = nameof(PriceRule.EndDate),
        ["lastAppliedAtUtc"] = nameof(PriceRule.LastAppliedAtUtc),
        ["createdOnUtc"] = nameof(PriceRule.CreatedOnUtc),
    };

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ListPriceRulesHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PaginatedList<PriceRuleListItemDto>> Handle(ListPriceRulesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<PriceRule> query = _db.PriceRules.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(r => r.Name.Contains(term));
        }

        if (request.IsActive.HasValue)
            query = query.Where(r => r.IsActive == request.IsActive.Value);

        if (request.StoreId.HasValue)
            query = query.Where(r => r.StoreId == null || r.StoreId == request.StoreId);

        var descending = string.Equals(request.Dir, "desc", StringComparison.OrdinalIgnoreCase);
        var ordered = query.ApplySort(request.Sort, descending, SortMap, defaultSort: q => q.OrderBy(r => r.Name));

        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var page = await PaginatedList<PriceRule>.CreateAsync(ordered, request.Page, pageSize, cancellationToken);

        var ids = page.Items.Select(r => r.Id).ToList();
        var affected = await _db.PriceRuleProductPrices.AsNoTracking()
            .Where(x => ids.Contains(x.PriceRuleId))
            .GroupBy(x => x.PriceRuleId)
            .Select(g => new { RuleId = g.Key, Count = g.Select(x => x.ProductId).Distinct().Count() })
            .ToDictionaryAsync(x => x.RuleId, x => x.Count, cancellationToken);

        var storeIds = page.Items.Where(r => r.StoreId.HasValue).Select(r => r.StoreId!.Value).Distinct().ToList();
        var storeNames = await _db.Stores.AsNoTracking()
            .Where(s => storeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var clock = await PriceRuleClock.LoadAsync(_db, cancellationToken);
        var now = _clock.UtcNow;

        var items = page.Items.Select(r => new PriceRuleListItemDto
        {
            Id = r.Id,
            StoreId = r.StoreId,
            StoreName = r.StoreId is Guid sid && storeNames.TryGetValue(sid, out var name) ? name : null,
            Name = r.Name,
            IsActive = r.IsActive,
            EffectiveState = PriceRuleStates.Of(r.IsActive, r.StartDate, r.EndDate, clock.TodayFor(r.StoreId, now)),
            DiscountType = r.DiscountType,
            DiscountAmount = r.DiscountAmount,
            Priority = r.Priority,
            StopFurtherRules = r.StopFurtherRules,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            ProductsAffected = affected.TryGetValue(r.Id, out var count) ? count : 0,
            LastAppliedAtUtc = r.LastAppliedAtUtc,
        }).ToList();

        return new PaginatedList<PriceRuleListItemDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

// ---- Get ------------------------------------------------------------------------------------------------

public record GetPriceRuleQuery(Guid Id) : IRequest<PriceRuleDto>;

public class GetPriceRuleHandler : IRequestHandler<GetPriceRuleQuery, PriceRuleDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetPriceRuleHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PriceRuleDto> Handle(GetPriceRuleQuery request, CancellationToken cancellationToken)
    {
        var rule = await _db.PriceRules.AsNoTracking()
            .Include(r => r.CustomerGroups)
            .Include(r => r.Categories)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PriceRule), request.Id);

        var clock = await PriceRuleClock.LoadAsync(_db, cancellationToken);
        return PriceRuleDto.From(rule, clock.TodayFor(rule.StoreId, _clock.UtcNow));
    }
}

// ---- Calculated prices of a rule ------------------------------------------------------------------------

public record ListPriceRuleProductsQuery(Guid Id, Guid? GroupId = null, int Page = 1, int PageSize = 25)
    : IRequest<PaginatedList<PriceRuleProductPriceDto>>;

public class ListPriceRuleProductsHandler : IRequestHandler<ListPriceRuleProductsQuery, PaginatedList<PriceRuleProductPriceDto>>
{
    private readonly IApplicationDbContext _db;

    public ListPriceRuleProductsHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<PriceRuleProductPriceDto>> Handle(ListPriceRuleProductsQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.PriceRules.AnyAsync(r => r.Id == request.Id, cancellationToken))
            throw new NotFoundException(nameof(PriceRule), request.Id);

        // IgnoreQueryFilters: a soft-deleted product keeps its row until the triggered rebuild drops it.
        var query =
            from x in _db.PriceRuleProductPrices.AsNoTracking()
            join p in _db.Products.IgnoreQueryFilters().AsNoTracking() on x.ProductId equals p.Id
            join v in _db.ProductVariants.IgnoreQueryFilters().AsNoTracking() on x.ProductVariantId equals (Guid?)v.Id into vj
            from v in vj.DefaultIfEmpty()
            join g in _db.CustomerGroups.IgnoreQueryFilters().AsNoTracking() on x.CustomerGroupId equals g.Id into gj
            from g in gj.DefaultIfEmpty()
            where x.PriceRuleId == request.Id
                  && (request.GroupId == null || x.CustomerGroupId == request.GroupId)
            orderby p.Name, v.DisplayOrder, x.CustomerGroupId
            select new PriceRuleProductPriceDto
            {
                ProductId = x.ProductId,
                ProductVariantId = x.ProductVariantId,
                Sku = v != null ? (v.Sku ?? p.Sku) : p.Sku,
                Name = p.Name,
                CustomerGroupId = x.CustomerGroupId,
                GroupName = g != null ? g.Name : PriceRuleStates.RetailGroupName,
                BasePrice = x.BasePrice,
                RulePrice = x.RulePrice,
            };

        return await PaginatedList<PriceRuleProductPriceDto>.CreateAsync(
            query, request.Page, Math.Clamp(request.PageSize, 1, 200), cancellationToken);
    }
}

// ---- Customer group lookup ------------------------------------------------------------------------------

/// <summary>Customer groups for the rule form, sorted by name, with the Retail sentinel first.</summary>
public record ListPriceRuleGroupOptionsQuery() : IRequest<List<PriceRuleGroupOptionDto>>;

public class ListPriceRuleGroupOptionsHandler : IRequestHandler<ListPriceRuleGroupOptionsQuery, List<PriceRuleGroupOptionDto>>
{
    private readonly IApplicationDbContext _db;

    public ListPriceRuleGroupOptionsHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<PriceRuleGroupOptionDto>> Handle(ListPriceRuleGroupOptionsQuery request, CancellationToken cancellationToken)
    {
        var groups = await _db.CustomerGroups.AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new PriceRuleGroupOptionDto { Id = g.Id, Name = g.Name, IsActive = g.IsActive })
            .ToListAsync(cancellationToken);

        groups.Insert(0, new PriceRuleGroupOptionDto
        {
            Id = PriceRuleCustomerGroup.RetailGroupId,
            Name = PriceRuleStates.RetailGroupName,
            IsActive = true,
        });
        return groups;
    }
}
