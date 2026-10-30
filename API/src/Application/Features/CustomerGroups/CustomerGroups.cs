using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerGroups;

// ---- Queries -----------------------------------------------------------------

/// <summary>Lists customer groups (with group prices), ordered for display.</summary>
public record ListCustomerGroupsQuery(bool ActiveOnly = false) : IRequest<List<CustomerGroupDto>>;

public class ListCustomerGroupsQueryHandler : IRequestHandler<ListCustomerGroupsQuery, List<CustomerGroupDto>>
{
    private readonly IApplicationDbContext _db;
    public ListCustomerGroupsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<CustomerGroupDto>> Handle(ListCustomerGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.CustomerGroups.AsNoTracking().Include(g => g.GroupPrices).AsSplitQuery();
        if (request.ActiveOnly)
            query = query.Where(g => g.IsActive);

        var groups = await query
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);
        return groups.Select(CustomerGroupDto.From).ToList();
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
    decimal? DiscountPercent = null, bool IsActive = true, int DisplayOrder = 0) : IRequest<CustomerGroupDto>;

public class CreateCustomerGroupCommandValidator : AbstractValidator<CreateCustomerGroupCommand>
{
    public CreateCustomerGroupCommandValidator()
    {
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
        };
        _db.CustomerGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);
        return CustomerGroupDto.From(group);
    }
}

/// <summary>Updates a customer group.</summary>
public record UpdateCustomerGroupCommand(
    Guid Id, string Name, string? Description, CustomerGroupPricingRuleType PricingRuleType,
    decimal? DiscountPercent, bool IsActive, int DisplayOrder) : IRequest<CustomerGroupDto>;

public class UpdateCustomerGroupCommandValidator : AbstractValidator<UpdateCustomerGroupCommand>
{
    public UpdateCustomerGroupCommandValidator()
    {
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
