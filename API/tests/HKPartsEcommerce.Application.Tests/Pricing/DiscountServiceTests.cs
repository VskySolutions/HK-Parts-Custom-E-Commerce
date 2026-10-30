using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Pricing;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Pricing;

/// <summary>
/// Coupon-gating behavior of the discount engine (WO-25 / REQ-PRP-002). A discount flagged
/// <see cref="Discount.RequiresCoupon"/> must never apply automatically — it only takes effect when
/// its id is passed as an "unlocked" (valid-coupon-bound) discount. Auto-apply discounts are
/// unaffected. Backed by the real SQL Server test database via <see cref="CatalogTestBase"/>.
/// </summary>
public class DiscountServiceTests : CatalogTestBase
{
    private Guid SeedDiscount(bool requiresCoupon, decimal value = 10m, string name = "Discount")
    {
        using var db = NewContext();
        var d = new Discount
        {
            Name = name,
            Scope = DiscountScope.CartTotal,
            Type = DiscountType.Percentage,
            Value = value,
            IsActive = true,
            RequiresCoupon = requiresCoupon,
        };
        db.Discounts.Add(d);
        db.SaveChanges();
        return d.Id;
    }

    private static IReadOnlyList<DiscountCartLine> Lines(decimal total) =>
        new List<DiscountCartLine> { new(Guid.NewGuid(), new List<Guid>(), total, 1) };

    private Guid SeedProductDiscount(Guid productId, decimal value, string name = "Product off")
    {
        using var db = NewContext();
        var d = new Discount
        {
            Name = name,
            Scope = DiscountScope.Product,
            ProductId = productId,
            Type = DiscountType.FixedAmount,
            Value = value,
            IsActive = true,
        };
        db.Discounts.Add(d);
        db.SaveChanges();
        return d.Id;
    }

    [Fact]
    public async Task CouponGatedDiscount_IsSkipped_WhenNoCouponUnlocksIt()
    {
        SeedDiscount(requiresCoupon: true, value: 10m);
        var svc = new DiscountService(NewContext());

        var result = await svc.EvaluateAsync(Lines(100m), 100m);

        Assert.Empty(result.Applied);
        Assert.Equal(0m, result.TotalDiscount);
    }

    [Fact]
    public async Task CouponGatedDiscount_IsApplied_WhenItsIdIsUnlocked()
    {
        var id = SeedDiscount(requiresCoupon: true, value: 10m);
        var svc = new DiscountService(NewContext());

        var result = await svc.EvaluateAsync(Lines(100m), 100m, new[] { id });

        var applied = Assert.Single(result.Applied);
        Assert.Equal(id, applied.DiscountId);
        Assert.Equal(10m, result.TotalDiscount); // 10% of 100
    }

    [Fact]
    public async Task AutoApplyDiscount_Applies_WithoutAnyCoupon()
    {
        SeedDiscount(requiresCoupon: false, value: 15m);
        var svc = new DiscountService(NewContext());

        var result = await svc.EvaluateAsync(Lines(100m), 100m);

        Assert.Single(result.Applied);
        Assert.Equal(15m, result.TotalDiscount);
    }

    [Fact]
    public async Task UnlockingOneGatedDiscount_DoesNotUnlockAnother()
    {
        var unlocked = SeedDiscount(requiresCoupon: true, value: 10m, name: "Unlocked");
        SeedDiscount(requiresCoupon: true, value: 25m, name: "Still locked");
        var svc = new DiscountService(NewContext());

        var result = await svc.EvaluateAsync(Lines(100m), 100m, new[] { unlocked });

        var applied = Assert.Single(result.Applied);
        Assert.Equal(unlocked, applied.DiscountId);
    }

    // ---- Per-line allocation (drives discount-before-tax; Scenarios 2 & 3) ----------------------

    [Fact]
    public async Task CartDiscount_IsAllocatedAcrossLines_ProportionalToLineValue()
    {
        SeedDiscount(requiresCoupon: false, value: 20m); // 20% of the $150 subtotal = $30
        var svc = new DiscountService(NewContext());
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var lines = new List<DiscountCartLine>
        {
            new(a, new List<Guid>(), 100m, 1),
            new(b, new List<Guid>(), 50m, 1),
        };

        var result = await svc.EvaluateAsync(lines, 150m);

        Assert.Equal(30m, result.TotalDiscount);
        Assert.Equal(new[] { 20m, 10m }, result.LineDiscounts); // 100/150 and 50/150 of $30
        Assert.Equal(result.TotalDiscount, result.LineDiscounts.Sum());
    }

    [Fact]
    public async Task ProductDiscount_IsAllocatedOnlyToThatProductsLine()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        SeedProductDiscount(a, value: 20m); // $20 off product A only
        var svc = new DiscountService(NewContext());
        var lines = new List<DiscountCartLine>
        {
            new(a, new List<Guid>(), 100m, 1),
            new(b, new List<Guid>(), 50m, 1),
        };

        var result = await svc.EvaluateAsync(lines, 150m);

        Assert.Equal(20m, result.TotalDiscount);
        Assert.Equal(new[] { 20m, 0m }, result.LineDiscounts); // all on A, none on B
    }
}
