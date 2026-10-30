using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Discounts;

/// <summary>Full view of a configurable discount rule (REQ-PRP-001).</summary>
public class DiscountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DiscountScope Scope { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public decimal? MinimumOrderValue { get; set; }
    public bool IsExclusive { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresCoupon { get; set; }

    public static DiscountDto From(Discount d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Scope = d.Scope,
        Type = d.Type,
        Value = d.Value,
        ProductId = d.ProductId,
        CategoryId = d.CategoryId,
        StartDateUtc = d.StartDateUtc,
        EndDateUtc = d.EndDateUtc,
        MinimumOrderValue = d.MinimumOrderValue,
        IsExclusive = d.IsExclusive,
        IsActive = d.IsActive,
        RequiresCoupon = d.RequiresCoupon,
    };
}
