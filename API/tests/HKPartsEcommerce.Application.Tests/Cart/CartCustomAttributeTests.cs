using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Cart;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Tests.Cart;

/// <summary>
/// The buyer's CustomInput values ride along with the cart line: validated on the way in (the
/// storefront blocks the same three cases, this is the guard for anything that bypasses it),
/// snapshotted with the attribute's name so a later rename can't rewrite history, and used as part of
/// the line's identity so two different engravings never collapse into one line.
/// </summary>
public class CartCustomAttributeTests : CatalogTestBase
{
    private sealed class StandardCommerceMode : ICommerceModeService
    {
        public Task<CommerceModeSettings> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CommerceModeSettings.Default);

        public Task<bool> IsInquiryOnlyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class NoGroupPricing : ICustomerGroupService
    {
        public Task<Guid?> GetCurrentGroupIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(null);

        public Task<Guid?> GetGroupIdForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(null);

        public Task<decimal> ResolvePriceAsync(
            Guid productId, Guid? variantId, decimal basePrice, Guid? groupId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(basePrice);

        public Task<IReadOnlyDictionary<GroupPriceKey, decimal>> ResolvePricesAsync(
            IReadOnlyCollection<GroupPriceRequest> items, Guid? groupId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<GroupPriceKey, decimal>>(
                items.ToDictionary(i => new GroupPriceKey(i.ProductId, i.VariantId), i => i.BasePrice));
    }

    private const string Session = "custom-input-session";

    private Guid SeedBuyable() => SeedProduct(p => { p.Name = "Ring"; p.IsPublished = true; p.Price = 50m; });

    private async Task<CartDto> AddAsync(Guid productId, params CustomAttributeInput[] values)
    {
        using var db = NewContext();
        var handler = new AddItemCommandHandler(db, new FakeCurrentUser(), new NoGroupPricing(), new StandardCommerceMode());
        return await handler.Handle(
            new AddItemCommand(productId, null, 1, Session, values.ToList()), CancellationToken.None);
    }

    [Fact]
    public async Task Typed_value_is_stored_on_the_line_with_the_attribute_name()
    {
        var productId = SeedBuyable();
        var attributeId = SeedCustomInputAttribute("Engraving", maxLength: 20, required: true);
        AssignAttribute(productId, attributeId);

        var cart = await AddAsync(productId, new CustomAttributeInput(attributeId, "  For Ana  "));

        var line = Assert.Single(cart.Items);
        var value = Assert.Single(line.CustomAttributes);
        Assert.Equal(attributeId, value.AttributeId);
        Assert.Equal("Engraving", value.Name);
        Assert.Equal("For Ana", value.Value);   // trimmed

        // Persisted as JSON on the line, so it survives to checkout independently of the attribute row.
        using var db = NewContext();
        var stored = db.CartItems.AsNoTracking().Single(i => i.Id == line.Id).CustomAttributesJson;
        Assert.Contains("Engraving", stored);
    }

    [Fact]
    public async Task Blank_mandatory_value_is_rejected()
    {
        var productId = SeedBuyable();
        AssignAttribute(productId, SeedCustomInputAttribute("Engraving", required: true));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => AddAsync(productId));

        Assert.Contains("Engraving", string.Join(" ", ex.Errors.SelectMany(e => e.Value)));
    }

    [Fact]
    public async Task Blank_optional_value_is_allowed_and_records_nothing()
    {
        var productId = SeedBuyable();
        AssignAttribute(productId, SeedCustomInputAttribute("Gift note"));

        var cart = await AddAsync(productId);

        Assert.Empty(Assert.Single(cart.Items).CustomAttributes);
    }

    [Fact]
    public async Task Value_longer_than_the_max_is_rejected()
    {
        var productId = SeedBuyable();
        var attributeId = SeedCustomInputAttribute("Engraving", maxLength: 5);
        AssignAttribute(productId, attributeId);

        await Assert.ThrowsAsync<ValidationException>(
            () => AddAsync(productId, new CustomAttributeInput(attributeId, "way too long")));
    }

    [Fact]
    public async Task Non_numeric_value_in_a_number_field_is_rejected()
    {
        var productId = SeedBuyable();
        var attributeId = SeedCustomInputAttribute("Ring size", ProductAttributeInputType.Number);
        AssignAttribute(productId, attributeId);

        await Assert.ThrowsAsync<ValidationException>(
            () => AddAsync(productId, new CustomAttributeInput(attributeId, "medium")));

        var cart = await AddAsync(productId, new CustomAttributeInput(attributeId, "7.5"));
        Assert.Equal("7.5", Assert.Single(Assert.Single(cart.Items).CustomAttributes).Value);
    }

    [Fact]
    public async Task Values_for_attributes_the_product_does_not_carry_are_ignored()
    {
        var productId = SeedBuyable();
        var strayId = SeedCustomInputAttribute("Not on this product");

        var cart = await AddAsync(productId, new CustomAttributeInput(strayId, "ignored"));

        Assert.Empty(Assert.Single(cart.Items).CustomAttributes);
    }

    [Fact]
    public async Task Different_typed_values_stay_separate_lines()
    {
        var productId = SeedBuyable();
        var attributeId = SeedCustomInputAttribute("Engraving");
        AssignAttribute(productId, attributeId);

        await AddAsync(productId, new CustomAttributeInput(attributeId, "For Ana"));
        var cart = await AddAsync(productId, new CustomAttributeInput(attributeId, "For Ben"));

        Assert.Equal(2, cart.Items.Count);
        Assert.All(cart.Items, i => Assert.Equal(1, i.Quantity));
    }

    [Fact]
    public async Task Identical_typed_values_merge_into_one_line()
    {
        var productId = SeedBuyable();
        var attributeId = SeedCustomInputAttribute("Engraving");
        AssignAttribute(productId, attributeId);

        await AddAsync(productId, new CustomAttributeInput(attributeId, "For Ana"));
        var cart = await AddAsync(productId, new CustomAttributeInput(attributeId, "For Ana"));

        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public void Serialization_round_trips_and_orders_stably()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var values = new List<CustomAttributeSelection>
        {
            new() { AttributeId = b, Name = "Gift note", Value = "Enjoy" },
            new() { AttributeId = a, Name = "Engraving", Value = "For Ana" },
        };

        var json = CustomAttributes.Serialize(values);
        var parsed = CustomAttributes.Parse(json);

        Assert.Equal(2, parsed.Count);
        // Ordered by attribute id, so the same set always serializes identically and the merge signature holds.
        Assert.Equal(values.OrderBy(v => v.AttributeId).Select(v => v.AttributeId), parsed.Select(v => v.AttributeId));
        Assert.Equal(CustomAttributes.Signature(json), CustomAttributes.Signature(CustomAttributes.Serialize(parsed)));
        Assert.Contains("Engraving: For Ana", CustomAttributes.Describe(json));
    }

    [Fact]
    public void Empty_and_malformed_payloads_read_as_no_values()
    {
        Assert.Null(CustomAttributes.Serialize(new List<CustomAttributeSelection>()));
        Assert.Empty(CustomAttributes.Parse(null));
        Assert.Empty(CustomAttributes.Parse("not json"));
        Assert.Equal(string.Empty, CustomAttributes.Describe(null));
    }
}
