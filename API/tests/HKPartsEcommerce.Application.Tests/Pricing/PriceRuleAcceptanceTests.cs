using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.PriceRules;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Customers;
using HKPartsEcommerce.Infrastructure.Persistence;
using HKPartsEcommerce.Infrastructure.Persistence.Interceptors;
using HKPartsEcommerce.Infrastructure.Pricing;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Pricing;

/// <summary>
/// The Price Rules acceptance list, end to end against the real SQL Server test DB: rules are saved, rebuilt
/// by the set-based engine into the flat table, and priced by the resolver.
/// Product P: list price $100, directly in "Brakes"; the shopper is retail (no group) unless stated.
/// </summary>
public class PriceRuleAcceptanceTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);
    private static readonly Guid Retail = PriceRuleCustomerGroup.RetailGroupId;

    private readonly FixedClock _clock = new(Now);
    private readonly Guid _brakes;
    private readonly Guid _p;

    public PriceRuleAcceptanceTests()
    {
        _brakes = SeedCategory("Brakes");
        _p = SeedProduct(p => { p.Name = "P"; p.Sku = "P-1"; p.Price = 100m; });
        AssignCategory(_p, _brakes);
    }

    // ---- helpers ------------------------------------------------------------------------------------------

    private void AssignCategory(Guid productId, Guid categoryId)
    {
        using var db = NewContext();
        db.ProductCategories.Add(new ProductCategory { ProductId = productId, CategoryId = categoryId });
        db.SaveChanges();
    }

    private Guid SeedRule(
        decimal amount,
        PriceRuleDiscountType type = PriceRuleDiscountType.Percent,
        Guid[]? groups = null,
        Guid[]? categories = null,
        bool active = true,
        int priority = 0,
        bool stop = false,
        DateOnly? start = null,
        DateOnly? end = null,
        bool rebuild = true)
    {
        Guid id;
        using (var db = NewContext())
        {
            var rule = new PriceRule
            {
                Name = $"Rule {Guid.NewGuid():N}",
                IsActive = active,
                Priority = priority,
                StopFurtherRules = stop,
                StartDate = start,
                EndDate = end,
                DiscountType = type,
                DiscountAmount = amount,
            };
            foreach (var g in groups ?? new[] { Retail })
                rule.CustomerGroups.Add(new PriceRuleCustomerGroup { CustomerGroupId = g });
            foreach (var c in categories ?? new[] { _brakes })
                rule.Categories.Add(new PriceRuleCategory { CategoryId = c });
            db.PriceRules.Add(rule);
            db.SaveChanges();
            id = rule.Id;
        }

        if (rebuild)
            Rebuild(id);
        return id;
    }

    private void Rebuild(Guid ruleId) => Engine().RebuildAsync(ruleId).GetAwaiter().GetResult();

    private PriceRuleEngine Engine() => new(NewContext(), _clock, NullLogger<PriceRuleEngine>.Instance);

    private PriceResolution Price(Guid productId, Guid? groupId = null, Guid? variantId = null, DateTime? at = null) =>
        new PriceResolver(NewContext(), _clock)
            .GetPriceAsync(productId, variantId, groupId, storeId: null, nowUtc: at ?? Now)
            .GetAwaiter().GetResult();

    private Guid SeedGroup(CustomerGroupPricingRuleType type = CustomerGroupPricingRuleType.None, decimal? percent = null)
    {
        using var db = NewContext();
        var group = new CustomerGroup { Name = $"Group {Guid.NewGuid():N}", PricingRuleType = type, DiscountPercent = percent };
        db.CustomerGroups.Add(group);
        db.SaveChanges();
        return group.Id;
    }

    private int FlatRows(Guid ruleId)
    {
        using var db = NewContext();
        return db.PriceRuleProductPrices.Count(x => x.PriceRuleId == ruleId);
    }

    // ---- 1–16 ---------------------------------------------------------------------------------------------

    [Fact]
    public void T01_percent_rule_on_brakes_for_retail()
    {
        SeedRule(10m);
        var price = Price(_p);
        Assert.Equal(90.00m, price.FinalPrice);
        Assert.True(price.RuleApplied);
        Assert.Equal(100m, price.BasePrice);
    }

    [Fact]
    public void T02_inactive_rule_does_not_apply()
    {
        var id = SeedRule(10m, active: false);
        Assert.Equal(100m, Price(_p).FinalPrice);
        Assert.Equal(0, FlatRows(id));
    }

    [Fact]
    public void T03_fixed_amount_rule_clamps_at_zero()
    {
        var a = SeedRule(15m, PriceRuleDiscountType.FixedAmount);
        Assert.Equal(85m, Price(_p).FinalPrice);

        using (var db = NewContext())
        {
            db.PriceRules.Remove(db.PriceRules.Single(r => r.Id == a));
            db.SaveChanges();
        }

        SeedRule(150m, PriceRuleDiscountType.FixedAmount);
        Assert.Equal(0m, Price(_p).FinalPrice);
    }

    [Fact]
    public void T04_lowest_of_sale_and_rule_wins()
    {
        using (var db = NewContext())
        {
            db.Products.Single(p => p.Id == _p).SalePrice = 85m;
            db.SaveChanges();
        }

        var ten = SeedRule(10m);
        var withTen = Price(_p);
        Assert.Equal(85m, withTen.FinalPrice);
        Assert.True(withTen.OnSale);
        Assert.False(withTen.RuleApplied);

        using (var db = NewContext())
        {
            db.PriceRules.Remove(db.PriceRules.Single(r => r.Id == ten));
            db.SaveChanges();
        }

        SeedRule(20m);
        var withTwenty = Price(_p);
        Assert.Equal(80m, withTwenty.FinalPrice);
        Assert.True(withTwenty.OnSale);
        Assert.True(withTwenty.RuleApplied);
    }

    [Fact]
    public void T05_rule_for_another_group_does_not_apply_to_retail()
    {
        var group3 = SeedGroup();
        SeedRule(10m, groups: new[] { group3 });
        Assert.Equal(100m, Price(_p).FinalPrice);
        Assert.Equal(90m, Price(_p, group3).FinalPrice);
    }

    [Fact]
    public void T06_rule_applies_to_the_group_base_price()
    {
        var group3 = SeedGroup(CustomerGroupPricingRuleType.FixedGroupPrice);
        using (var db = NewContext())
        {
            db.CustomerGroupPrices.Add(new CustomerGroupPrice { CustomerGroupId = group3, ProductId = _p, Price = 95m });
            db.SaveChanges();
        }

        SeedRule(10m, groups: new[] { group3 });
        var price = Price(_p, group3);
        Assert.Equal(95m, price.BasePrice);
        Assert.Equal(85.50m, price.FinalPrice);
    }

    [Fact]
    public void T07_future_rule_starts_tomorrow_without_a_rebuild()
    {
        SeedRule(10m, start: Today.AddDays(1));
        Assert.Equal(100m, Price(_p).FinalPrice);
        Assert.Equal(90m, Price(_p, at: Now.Date.AddDays(1)).FinalPrice);               // 00:00 tomorrow
        Assert.Equal(90m, Price(_p, at: Now.Date.AddDays(2).AddTicks(-1)).FinalPrice);  // 23:59:59 tomorrow
    }

    [Fact]
    public void T08_rule_that_ended_yesterday_does_not_apply()
    {
        var id = SeedRule(10m, start: Today.AddDays(-10), end: Today.AddDays(-1));
        Assert.Equal(100m, Price(_p).FinalPrice);
        Assert.Equal(0, FlatRows(id));
    }

    [Fact]
    public void T09_only_direct_category_assignment_counts()
    {
        var pads = SeedCategoryUnder("Pads", _brakes);
        var child = SeedProduct(p => { p.Name = "Pad"; p.Price = 100m; });
        AssignCategory(child, pads);

        SeedRule(10m); // ticks only Brakes
        Assert.Equal(100m, Price(child).FinalPrice);
    }

    private Guid SeedCategoryUnder(string name, Guid parentId)
    {
        using var db = NewContext();
        var category = new Category { Name = name, ParentId = parentId };
        db.Categories.Add(category);
        db.SaveChanges();
        return category.Id;
    }

    [Fact]
    public void T10_variants_of_a_matched_product_are_discounted_from_their_own_price()
    {
        using (var db = NewContext())
        {
            db.Products.Single(p => p.Id == _p).ProductType = ProductType.WithVariants;
            db.SaveChanges();
        }

        var v1 = SeedVariant(_p, v => v.Price = 100m);
        var v2 = SeedVariant(_p, v => v.Price = 120m);
        SeedRule(10m);

        Assert.Equal(90m, Price(_p, variantId: v1).FinalPrice);
        Assert.Equal(108m, Price(_p, variantId: v2).FinalPrice);
    }

    [Fact]
    public void T11_gift_cards_are_never_discounted()
    {
        var gift = SeedProduct(p => { p.Name = "Gift"; p.ProductType = ProductType.GiftCard; p.Price = 100m; });
        AssignCategory(gift, _brakes);

        var id = SeedRule(10m);
        Assert.Equal(100m, Price(gift).FinalPrice);
        using var db = NewContext();
        Assert.False(db.PriceRuleProductPrices.Any(x => x.PriceRuleId == id && x.ProductId == gift));
    }

    [Fact]
    public void T12_rules_do_not_stack_and_stop_ends_the_walk()
    {
        var a = SeedRule(10m, priority: 0);
        SeedRule(20m, priority: 1);
        Assert.Equal(80m, Price(_p).FinalPrice);

        using (var db = NewContext())
        {
            db.PriceRules.Single(r => r.Id == a).StopFurtherRules = true;
            db.SaveChanges();
        }

        Rebuild(a);
        Assert.Equal(90m, Price(_p).FinalPrice);
    }

    [Fact]
    public async Task T13_deleting_the_rule_reverts_immediately()
    {
        var id = SeedRule(10m);
        Assert.Equal(90m, Price(_p).FinalPrice);

        await new DeletePriceRuleHandler(NewContext(), new FakeCurrentUser(), _clock)
            .Handle(new DeletePriceRuleCommand(id), default);

        Assert.Equal(100m, Price(_p).FinalPrice);
        Assert.Equal(0, FlatRows(id));
    }

    [Fact]
    public async Task T14_product_added_to_brakes_later_is_priced_by_the_triggered_rebuild()
    {
        SeedRule(10m);
        var late = SeedProduct(p => { p.Name = "Late"; p.Price = 100m; });
        Assert.Equal(100m, Price(late).FinalPrice);

        // The save interceptor turns the category assignment into a queued product rebuild...
        var queue = new RecordingQueue();
        var options = new DbContextOptionsBuilder<AppDbContext>(_options)
            .AddInterceptors(new PriceRuleTriggerInterceptor(queue))
            .Options;
        using (var db = new AppDbContext(options))
        {
            db.Database.UseTransaction(_transaction);
            db.ProductCategories.Add(new ProductCategory { ProductId = late, CategoryId = _brakes });
            await db.SaveChangesAsync();
        }

        Assert.Contains(late, queue.Products);

        // ...which the consumer runs (no nightly job involved).
        await Engine().RebuildForProductsAsync(queue.Products.ToList());
        Assert.Equal(90m, Price(late).FinalPrice);
    }

    [Fact]
    public void T15_product_in_two_selected_categories_gets_one_row_per_group()
    {
        var rotors = SeedCategory("Rotors");
        AssignCategory(_p, rotors);
        var wholesale = SeedGroup();

        var id = SeedRule(10m, groups: new[] { Retail, wholesale }, categories: new[] { _brakes, rotors });

        using var db = NewContext();
        var rows = db.PriceRuleProductPrices.Where(x => x.PriceRuleId == id && x.ProductId == _p).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(r => r.CustomerGroupId).Distinct().Count());
    }

    [Fact]
    public async Task Guests_get_rule_prices_through_the_storefront_price_funnel()
    {
        SeedRule(10m);
        var db = NewContext();
        var groups = new CustomerGroupService(db, new FakeCurrentUser(), new PriceResolver(db, _clock));

        var price = await groups.ResolvePriceAsync(_p, null, 100m, groupId: null);
        Assert.Equal(90m, price);
    }

    [Fact]
    public async Task Preview_counts_products_and_samples_prices()
    {
        var wholesale = SeedGroup();
        var result = await Engine().PreviewAsync(new PriceRuleDraft(
            null, true, null, PriceRuleDiscountType.Percent, 10m, new[] { Retail, wholesale }, new[] { _brakes }));

        Assert.Equal(1, result.ProductCount);
        Assert.Equal(2, result.PriceRowCount);
        Assert.All(result.Sample, r => Assert.Equal(90m, r.RulePrice));
        Assert.Contains(result.Sample, r => r.GroupName == PriceRuleStates.RetailGroupName);
    }

    // ---- 17: validation -----------------------------------------------------------------------------------

    private CreatePriceRuleCommand ValidCommand() => new(
        StoreId: null, Name: "Brakes 10%", Description: null, IsActive: true, Priority: 0, StopFurtherRules: false,
        StartDate: null, EndDate: null, DiscountType: PriceRuleDiscountType.Percent, DiscountAmount: 10m,
        CustomerGroupIds: new List<Guid> { Retail }, CategoryIds: new List<Guid> { _brakes });

    public static IEnumerable<object[]> InvalidCommands() => new[]
    {
        new object[] { "percent 150", "DiscountAmount" },
        new object[] { "end before start", "EndDate" },
        new object[] { "no groups", "CustomerGroupIds" },
        new object[] { "no categories", "CategoryIds" },
        new object[] { "blank name", "Name" },
        new object[] { "unknown category", "CategoryIds" },
        new object[] { "unknown group", "CustomerGroupIds" },
    };

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public async Task T17_invalid_rules_are_rejected(string scenario, string field)
    {
        var command = scenario switch
        {
            "percent 150" => ValidCommand() with { DiscountAmount = 150m },
            "end before start" => ValidCommand() with { StartDate = Today, EndDate = Today.AddDays(-1) },
            "no groups" => ValidCommand() with { CustomerGroupIds = new List<Guid>() },
            "no categories" => ValidCommand() with { CategoryIds = new List<Guid>() },
            "blank name" => ValidCommand() with { Name = "  " },
            "unknown category" => ValidCommand() with { CategoryIds = new List<Guid> { Guid.NewGuid() } },
            "unknown group" => ValidCommand() with { CustomerGroupIds = new List<Guid> { Guid.NewGuid() } },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        var result = await new CreatePriceRuleValidator(NewContext()).ValidateAsync(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public async Task T17_valid_rule_passes()
    {
        var result = await new CreatePriceRuleValidator(NewContext()).ValidateAsync(ValidCommand());
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    // ---- 18: optimistic concurrency -----------------------------------------------------------------------

    [Fact]
    public async Task T18_stale_row_version_is_a_conflict()
    {
        var queue = new RecordingQueue();
        var created = await new CreatePriceRuleHandler(NewContext(), queue, new FakeCurrentUser(), _clock)
            .Handle(ValidCommand(), default);
        Assert.Contains(created.Id, queue.Rules);

        UpdatePriceRuleCommand Update(string rowVersion) => new(
            created.Id, null, "Renamed", null, true, 0, false, null, null, PriceRuleDiscountType.Percent, 15m,
            new List<Guid> { Retail }, new List<Guid> { _brakes }, rowVersion);

        var updated = await new UpdatePriceRuleHandler(NewContext(), queue, new FakeCurrentUser(), _clock)
            .Handle(Update(created.RowVersion), default);
        Assert.NotEqual(created.RowVersion, updated.RowVersion);

        // Saving again with the original (now stale) version is refused.
        await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdatePriceRuleHandler(NewContext(), queue, new FakeCurrentUser(), _clock)
                .Handle(Update(created.RowVersion), default));
    }

    [Fact]
    public async Task Create_and_update_write_before_after_audit_rows()
    {
        var created = await new CreatePriceRuleHandler(NewContext(), new RecordingQueue(), new FakeCurrentUser(), _clock)
            .Handle(ValidCommand(), default);

        using var db = NewContext();
        var audit = db.AuditTrails.Single(a => a.EntityId == created.Id.ToString() && a.Action == "PriceRule.Created");
        Assert.Contains("\"after\"", audit.MetadataJson);
        Assert.Contains("Brakes 10%", audit.MetadataJson);
    }

    private sealed class RecordingQueue : IPriceRuleRebuildQueue
    {
        public List<Guid> Rules { get; } = new();
        public HashSet<Guid> Products { get; } = new();
        public int AllCount { get; private set; }

        public void EnqueueRule(Guid ruleId) => Rules.Add(ruleId);
        public void EnqueueAll() => AllCount++;
        public void EnqueueForProducts(IEnumerable<Guid> productIds) => Products.UnionWith(productIds);
    }
}
