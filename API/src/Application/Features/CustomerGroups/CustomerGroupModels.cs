using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerGroups;

/// <summary>A customer pricing group with its rule and fixed group prices (REQ-CUS-003).</summary>
public class CustomerGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CustomerGroupPricingRuleType PricingRuleType { get; set; }
    public decimal? DiscountPercent { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public List<CustomerGroupPriceDto> GroupPrices { get; set; } = new();

    public static CustomerGroupDto From(CustomerGroup g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        Description = g.Description,
        PricingRuleType = g.PricingRuleType,
        DiscountPercent = g.DiscountPercent,
        IsActive = g.IsActive,
        DisplayOrder = g.DisplayOrder,
        GroupPrices = g.GroupPrices.Select(CustomerGroupPriceDto.From).ToList(),
    };
}

/// <summary>A group's identity only — for pickers and the customer list/detail.</summary>
public class CustomerGroupBriefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CustomerGroupPricingRuleType PricingRuleType { get; set; }
    public decimal? DiscountPercent { get; set; }

    public static CustomerGroupBriefDto From(CustomerGroup g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        PricingRuleType = g.PricingRuleType,
        DiscountPercent = g.DiscountPercent,
    };
}

public class CustomerGroupPriceDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Price { get; set; }

    public static CustomerGroupPriceDto From(CustomerGroupPrice g) => new()
    {
        Id = g.Id,
        ProductId = g.ProductId,
        ProductVariantId = g.ProductVariantId,
        Price = g.Price,
    };
}

/// <summary>An input group-price row for the set-group-prices command.</summary>
public record CustomerGroupPriceInput(Guid ProductId, Guid? ProductVariantId, decimal Price);
