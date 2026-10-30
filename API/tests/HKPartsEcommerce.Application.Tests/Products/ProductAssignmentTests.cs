using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class ProductAssignmentTests : CatalogTestBase
{
    // ---- Categories -------------------------------------------------------------------------------

    [Fact]
    public async Task SetCategories_assigns_requested_set_in_order()
    {
        var productId = SeedProduct();
        var catA = SeedCategory("A");
        var catB = SeedCategory("B");

        var dto = await new SetProductCategoriesCommandHandler(NewContext()).Handle(
            new SetProductCategoriesCommand(productId, new() { catB, catA }),
            CancellationToken.None);

        // DisplayOrder follows request order: catB (0) then catA (1); CategoryIds is ordered by DisplayOrder.
        Assert.Equal(new[] { catB, catA }, dto.CategoryIds.ToArray());
    }

    [Fact]
    public async Task SetCategories_replaces_previous_assignments()
    {
        var productId = SeedProduct();
        var catA = SeedCategory("A");
        var catB = SeedCategory("B");
        await new SetProductCategoriesCommandHandler(NewContext()).Handle(
            new SetProductCategoriesCommand(productId, new() { catA, catB }), CancellationToken.None);

        var dto = await new SetProductCategoriesCommandHandler(NewContext()).Handle(
            new SetProductCategoriesCommand(productId, new() { catB }), CancellationToken.None);

        Assert.Equal(new[] { catB }, dto.CategoryIds.ToArray());
    }

    [Fact]
    public async Task SetCategories_deduplicates_ids()
    {
        var productId = SeedProduct();
        var catA = SeedCategory("A");

        var dto = await new SetProductCategoriesCommandHandler(NewContext()).Handle(
            new SetProductCategoriesCommand(productId, new() { catA, catA }), CancellationToken.None);

        Assert.Single(dto.CategoryIds);
    }

    [Fact]
    public async Task SetCategories_empty_list_clears_assignments()
    {
        var productId = SeedProduct();
        var catA = SeedCategory("A");
        await new SetProductCategoriesCommandHandler(NewContext()).Handle(
            new SetProductCategoriesCommand(productId, new() { catA }), CancellationToken.None);

        var dto = await new SetProductCategoriesCommandHandler(NewContext()).Handle(
            new SetProductCategoriesCommand(productId, new()), CancellationToken.None);

        Assert.Empty(dto.CategoryIds);
    }

    [Fact]
    public async Task SetCategories_throws_NotFound_for_unknown_category()
    {
        var productId = SeedProduct();

        await Assert.ThrowsAsync<NotFoundException>(() => new SetProductCategoriesCommandHandler(NewContext()).Handle(
            new SetProductCategoriesCommand(productId, new() { Guid.NewGuid() }), CancellationToken.None));
    }

    [Fact]
    public async Task SetCategories_throws_NotFound_when_product_missing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new SetProductCategoriesCommandHandler(NewContext()).Handle(
            new SetProductCategoriesCommand(Guid.NewGuid(), new()), CancellationToken.None));
    }

    // ---- Tags -------------------------------------------------------------------------------------

    [Fact]
    public async Task SetTags_creates_new_tags()
    {
        var productId = SeedProduct();

        var dto = await new SetProductTagsCommandHandler(NewContext()).Handle(
            new SetProductTagsCommand(productId, new() { "Summer", "Sale" }), CancellationToken.None);

        Assert.Equal(2, dto.Tags.Count);
        Assert.Contains(dto.Tags, t => t.Name == "Summer");
    }

    [Fact]
    public async Task SetTags_deduplicates_case_insensitively()
    {
        var productId = SeedProduct();

        var dto = await new SetProductTagsCommandHandler(NewContext()).Handle(
            new SetProductTagsCommand(productId, new() { "Red", "red", "  RED  " }), CancellationToken.None);

        Assert.Single(dto.Tags);

        await using var db = NewContext();
        Assert.Equal(1, await db.ProductTags.CountAsync());
    }

    [Fact]
    public async Task SetTags_reuses_existing_tag_row()
    {
        var firstProduct = SeedProduct();
        var secondProduct = SeedProduct();
        await new SetProductTagsCommandHandler(NewContext()).Handle(
            new SetProductTagsCommand(firstProduct, new() { "Shared" }), CancellationToken.None);

        await new SetProductTagsCommandHandler(NewContext()).Handle(
            new SetProductTagsCommand(secondProduct, new() { "shared" }), CancellationToken.None);

        await using var db = NewContext();
        Assert.Equal(1, await db.ProductTags.CountAsync());
        Assert.Equal(2, await db.ProductTagMappings.CountAsync());
    }

    [Fact]
    public async Task SetTags_replaces_previous_tags()
    {
        var productId = SeedProduct();
        await new SetProductTagsCommandHandler(NewContext()).Handle(
            new SetProductTagsCommand(productId, new() { "Old" }), CancellationToken.None);

        var dto = await new SetProductTagsCommandHandler(NewContext()).Handle(
            new SetProductTagsCommand(productId, new() { "New" }), CancellationToken.None);

        Assert.Single(dto.Tags);
        Assert.Equal("New", dto.Tags[0].Name);
    }

    // ---- Attributes -------------------------------------------------------------------------------

    [Fact]
    public async Task SetAttributes_assigns_requested_set()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var (colorId, _) = SeedAttribute("Color", "Red");
        var (sizeId, _) = SeedAttribute("Size", "S");

        var dto = await new SetProductAttributesCommandHandler(NewContext()).Handle(
            new SetProductAttributesCommand(productId, new() { colorId, sizeId }), CancellationToken.None);

        Assert.Equal(new[] { colorId, sizeId }, dto.AttributeIds.ToArray());
    }

    [Fact]
    public async Task SetAttributes_deduplicates_ids()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var (colorId, _) = SeedAttribute("Color", "Red");

        var dto = await new SetProductAttributesCommandHandler(NewContext()).Handle(
            new SetProductAttributesCommand(productId, new() { colorId, colorId }), CancellationToken.None);

        Assert.Single(dto.AttributeIds);
    }

    [Fact]
    public async Task SetAttributes_replaces_previous_assignments()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var (colorId, _) = SeedAttribute("Color", "Red");
        var (sizeId, _) = SeedAttribute("Size", "S");
        await new SetProductAttributesCommandHandler(NewContext()).Handle(
            new SetProductAttributesCommand(productId, new() { colorId }), CancellationToken.None);

        var dto = await new SetProductAttributesCommandHandler(NewContext()).Handle(
            new SetProductAttributesCommand(productId, new() { sizeId }), CancellationToken.None);

        Assert.Equal(new[] { sizeId }, dto.AttributeIds.ToArray());
    }

    [Fact]
    public async Task SetAttributes_throws_NotFound_for_unknown_attribute()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);

        await Assert.ThrowsAsync<NotFoundException>(() => new SetProductAttributesCommandHandler(NewContext()).Handle(
            new SetProductAttributesCommand(productId, new() { Guid.NewGuid() }), CancellationToken.None));
    }
}
