using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Customers;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>
/// Customer Group price resolution (REQ-CUS-003) — the rule that determines what a group member pays and
/// is shared by the storefront, cart and checkout. Backed by the real SQL Server test DB.
/// </summary>
public class CustomerGroupPricingTests : CatalogTestBase
{
    private Guid SeedGroup(CustomerGroupPricingRuleType rule, decimal? discount = null, bool active = true)
    {
        using var db = NewContext();
        var group = new CustomerGroup
        {
            Name = $"Group {Guid.NewGuid():N}",
            PricingRuleType = rule,
            DiscountPercent = discount,
            IsActive = active,
        };
        db.CustomerGroups.Add(group);
        db.SaveChanges();
        return group.Id;
    }

    private void SeedGroupPrice(Guid groupId, Guid productId, Guid? variantId, decimal price)
    {
        using var db = NewContext();
        db.CustomerGroupPrices.Add(new CustomerGroupPrice
        {
            CustomerGroupId = groupId,
            ProductId = productId,
            ProductVariantId = variantId,
            Price = price,
        });
        db.SaveChanges();
    }

    private CustomerGroupService NewService() => new(NewContext(), new FakeCurrentUser());

    [Fact]
    public async Task No_group_returns_base_price()
    {
        var svc = NewService();
        var price = await svc.ResolvePriceAsync(Guid.NewGuid(), null, 100m, groupId: null);
        Assert.Equal(100m, price);
    }

    [Fact]
    public async Task Percentage_discount_reduces_price()
    {
        var groupId = SeedGroup(CustomerGroupPricingRuleType.PercentageDiscount, discount: 10m);

        var price = await NewService().ResolvePriceAsync(Guid.NewGuid(), null, 100m, groupId);

        Assert.Equal(90m, price);
    }

    [Fact]
    public async Task Percentage_discount_rounds_to_two_places()
    {
        var groupId = SeedGroup(CustomerGroupPricingRuleType.PercentageDiscount, discount: 15m);

        // 9.99 * 0.85 = 8.4915 -> 8.49
        var price = await NewService().ResolvePriceAsync(Guid.NewGuid(), null, 9.99m, groupId);

        Assert.Equal(8.49m, price);
    }

    [Fact]
    public async Task Inactive_group_returns_base_price()
    {
        var groupId = SeedGroup(CustomerGroupPricingRuleType.PercentageDiscount, discount: 50m, active: false);

        var price = await NewService().ResolvePriceAsync(Guid.NewGuid(), null, 100m, groupId);

        Assert.Equal(100m, price);
    }

    [Fact]
    public async Task Fixed_product_price_applies()
    {
        var productId = Guid.NewGuid();
        var groupId = SeedGroup(CustomerGroupPricingRuleType.FixedGroupPrice);
        SeedGroupPrice(groupId, productId, variantId: null, price: 42m);

        var price = await NewService().ResolvePriceAsync(productId, null, 100m, groupId);

        Assert.Equal(42m, price);
    }

    [Fact]
    public async Task Fixed_price_above_base_still_applies()
    {
        // A fixed group price is authoritative — the admin set it deliberately, so it applies even above base.
        var productId = Guid.NewGuid();
        var groupId = SeedGroup(CustomerGroupPricingRuleType.FixedGroupPrice);
        SeedGroupPrice(groupId, productId, variantId: null, price: 120m);

        var price = await NewService().ResolvePriceAsync(productId, null, 100m, groupId);

        Assert.Equal(120m, price);
    }

    [Fact]
    public async Task Variant_fixed_price_overrides_product_level()
    {
        // AC-CUS-003.4: a variant-specific fixed price wins over the product-level one for that variant.
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var groupId = SeedGroup(CustomerGroupPricingRuleType.FixedGroupPrice);
        SeedGroupPrice(groupId, productId, variantId: null, price: 80m);
        SeedGroupPrice(groupId, productId, variantId: variantId, price: 60m);

        var price = await NewService().ResolvePriceAsync(productId, variantId, 100m, groupId);

        Assert.Equal(60m, price);
    }

    [Fact]
    public async Task Variant_without_own_price_falls_back_to_product_level()
    {
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var groupId = SeedGroup(CustomerGroupPricingRuleType.FixedGroupPrice);
        SeedGroupPrice(groupId, productId, variantId: null, price: 80m);

        var price = await NewService().ResolvePriceAsync(productId, variantId, 100m, groupId);

        Assert.Equal(80m, price);
    }

    [Fact]
    public async Task Fixed_rule_without_matching_row_returns_base()
    {
        var groupId = SeedGroup(CustomerGroupPricingRuleType.FixedGroupPrice);

        var price = await NewService().ResolvePriceAsync(Guid.NewGuid(), null, 100m, groupId);

        Assert.Equal(100m, price);
    }

    [Fact]
    public async Task Bulk_resolution_returns_every_key_and_mixes_matched_and_base()
    {
        var matched = Guid.NewGuid();
        var unmatched = Guid.NewGuid();
        var groupId = SeedGroup(CustomerGroupPricingRuleType.FixedGroupPrice);
        SeedGroupPrice(groupId, matched, variantId: null, price: 30m);

        var svc = NewService();
        var result = await svc.ResolvePricesAsync(new[]
        {
            new GroupPriceRequest(matched, null, 100m),
            new GroupPriceRequest(unmatched, null, 55m),
        }, groupId);

        Assert.Equal(30m, result[new GroupPriceKey(matched, null)]);
        Assert.Equal(55m, result[new GroupPriceKey(unmatched, null)]);
    }
}
