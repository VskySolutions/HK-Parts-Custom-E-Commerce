using FluentValidation;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HKPartsEcommerce.Application.Features.CustomerGroups;

// ---- Queries -----------------------------------------------------------------

/// <summary>Lists customer groups (with group prices), ordered for display.</summary>
public record ListCustomerGroupsQuery(bool ActiveOnly = false, int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool SortDescending = false) : IRequest<PaginatedList<CustomerGroupDto>>;

public class ListCustomerGroupsQueryHandler : IRequestHandler<ListCustomerGroupsQuery, PaginatedList<CustomerGroupDto>>
{
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["displayOrder"] = "DisplayOrder",
        ["discountPercent"] = "DiscountPercent",
        ["pricingRuleType"] = "PricingRuleType",
        ["isActive"] = "IsActive"
    };

    private readonly IApplicationDbContext _db;
    public ListCustomerGroupsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CustomerGroupDto>> Handle(ListCustomerGroupsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<CustomerGroup> query = _db.CustomerGroups.AsNoTracking().Include(g => g.GroupPrices).AsSplitQuery().Where(g => !g.Deleted);

        if (request.ActiveOnly)
            query = query.Where(g => g.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(g =>
                g.Name.Contains(term) ||
                (g.Description != null && g.Description.Contains(term)) ||
                (g.DiscountPercent.HasValue &&
                    g.DiscountPercent.Value.ToString().Contains(term)) ||
                g.PricingRuleType.ToString().Contains(term) ||
                g.DisplayOrder.ToString().Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap, defaultSort: q => q.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name));

        var page = await PaginatedList<CustomerGroup>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(CustomerGroupDto.From).ToList();
        return new PaginatedList<CustomerGroupDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

/// <summary>Gets one customer group.</summary>
public record GetCustomerGroupQuery(Guid Id) : IRequest<CustomerGroupDto>;

public class GetCustomerGroupQueryHandler : IRequestHandler<GetCustomerGroupQuery, CustomerGroupDto>
{
    private readonly IApplicationDbContext _db;
    public GetCustomerGroupQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerGroupDto> Handle(GetCustomerGroupQuery request, CancellationToken cancellationToken)
    {
        var group = await _db.CustomerGroups.AsNoTracking().Include(g => g.GroupPrices)
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerGroup), request.Id);
        return CustomerGroupDto.From(group);
    }
}

// ---- Commands ----------------------------------------------------------------

/// <summary>Creates a customer group with a pricing rule (AC-CUS-003.1).</summary>
public record CreateCustomerGroupCommand(
    string Name, string? Description, CustomerGroupPricingRuleType PricingRuleType,
    decimal? DiscountPercent = null, bool IsActive = true, int DisplayOrder = 0,
    TaxProviderType? TaxMethod = null) : IRequest<CustomerGroupDto>;

public class CreateCustomerGroupCommandValidator : AbstractValidator<CreateCustomerGroupCommand>
{
    public CreateCustomerGroupCommandValidator()
    {
        RuleFor(x => x.TaxMethod).IsInEnum().When(x => x.TaxMethod.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(512);
        RuleFor(x => x.DiscountPercent).NotNull().InclusiveBetween(0, 100)
            .When(x => x.PricingRuleType == CustomerGroupPricingRuleType.PercentageDiscount);
    }
}

public class CreateCustomerGroupCommandHandler : IRequestHandler<CreateCustomerGroupCommand, CustomerGroupDto>
{
    private readonly IApplicationDbContext _db;
    public CreateCustomerGroupCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerGroupDto> Handle(CreateCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var group = new CustomerGroup
        {
            Name = request.Name,
            Description = request.Description,
            PricingRuleType = request.PricingRuleType,
            DiscountPercent = request.PricingRuleType == CustomerGroupPricingRuleType.PercentageDiscount ? request.DiscountPercent : null,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            TaxMethod = request.TaxMethod,
        };
        _db.CustomerGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);
        return CustomerGroupDto.From(group);
    }
}

/// <summary>Updates a customer group.</summary>
public record UpdateCustomerGroupCommand(
    Guid Id, string Name, string? Description, CustomerGroupPricingRuleType PricingRuleType,
    decimal? DiscountPercent, bool IsActive, int DisplayOrder,
    TaxProviderType? TaxMethod = null) : IRequest<CustomerGroupDto>;

public class UpdateCustomerGroupCommandValidator : AbstractValidator<UpdateCustomerGroupCommand>
{
    public UpdateCustomerGroupCommandValidator()
    {
        RuleFor(x => x.TaxMethod).IsInEnum().When(x => x.TaxMethod.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(512);
        RuleFor(x => x.DiscountPercent).NotNull().InclusiveBetween(0, 100)
            .When(x => x.PricingRuleType == CustomerGroupPricingRuleType.PercentageDiscount);
    }
}

public class UpdateCustomerGroupCommandHandler : IRequestHandler<UpdateCustomerGroupCommand, CustomerGroupDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateCustomerGroupCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerGroupDto> Handle(UpdateCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _db.CustomerGroups.Include(g => g.GroupPrices).FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerGroup), request.Id);

        group.Name = request.Name;
        group.Description = request.Description;
        group.PricingRuleType = request.PricingRuleType;
        group.DiscountPercent = request.PricingRuleType == CustomerGroupPricingRuleType.PercentageDiscount ? request.DiscountPercent : null;
        group.IsActive = request.IsActive;
        group.DisplayOrder = request.DisplayOrder;
        group.TaxMethod = request.TaxMethod;
        await _db.SaveChangesAsync(cancellationToken);
        return CustomerGroupDto.From(group);
    }
}

/// <summary>Removes a customer group (soft delete); members fall back to base pricing.</summary>
public record DeleteCustomerGroupCommand(Guid Id) : IRequest;

public class DeleteCustomerGroupCommandHandler : IRequestHandler<DeleteCustomerGroupCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteCustomerGroupCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCustomerGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _db.CustomerGroups.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);
        if (group is null)
            return;

        // Detach members first: Customer.CustomerGroupId is Restrict, so a lingering reference would block
        // the delete, and a soft-deleted group must not keep pricing anyone.
        var members = await _db.Customers.Where(c => c.CustomerGroupId == group.Id).ToListAsync(cancellationToken);
        foreach (var member in members)
            member.CustomerGroupId = null;

        _db.CustomerGroups.Remove(group);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Replaces the fixed group prices of a group (AC-CUS-003.4).</summary>
public record SetGroupPricesCommand(Guid GroupId, List<CustomerGroupPriceInput> Prices) : IRequest<CustomerGroupDto>;

public class SetGroupPricesCommandValidator : AbstractValidator<SetGroupPricesCommand>
{
    public SetGroupPricesCommandValidator()
    {
        RuleForEach(x => x.Prices).ChildRules(p => p.RuleFor(x => x.Price).GreaterThanOrEqualTo(0));
        // The unique index (group, product, variant) rejects duplicates at the DB level; fail with a 400 first.
        RuleFor(x => x.Prices)
            .Must(prices => prices is null
                || prices.GroupBy(p => (p.ProductId, p.ProductVariantId)).All(g => g.Count() == 1))
            .WithMessage("Each product/variant may only have one group price.");
    }
}

public class SetGroupPricesCommandHandler : IRequestHandler<SetGroupPricesCommand, CustomerGroupDto>
{
    private readonly IApplicationDbContext _db;
    public SetGroupPricesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerGroupDto> Handle(SetGroupPricesCommand request, CancellationToken cancellationToken)
    {
        var group = await _db.CustomerGroups.Include(g => g.GroupPrices).FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerGroup), request.GroupId);

        foreach (var existing in group.GroupPrices.ToList())
            _db.CustomerGroupPrices.Remove(existing);

        foreach (var input in request.Prices ?? new List<CustomerGroupPriceInput>())
            _db.CustomerGroupPrices.Add(new CustomerGroupPrice
            {
                CustomerGroupId = group.Id,
                ProductId = input.ProductId,
                ProductVariantId = input.ProductVariantId,
                Price = input.Price,
            });

        await _db.SaveChangesAsync(cancellationToken);
        var reloaded = await _db.CustomerGroups.AsNoTracking().Include(g => g.GroupPrices).FirstAsync(g => g.Id == group.Id, cancellationToken);
        return CustomerGroupDto.From(reloaded);
    }
}
