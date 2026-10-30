using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class GenerateVariantsTests : CatalogTestBase
{
    private GenerateVariantsCommandHandler NewHandler() => new(NewContext());

    [Fact]
    public async Task Generate_throws_NotFound_when_product_missing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new GenerateVariantsCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Generate_throws_Conflict_when_no_attributes_assigned()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);

        await Assert.ThrowsAsync<ConflictException>(() => NewHandler().Handle(
            new GenerateVariantsCommand(productId), CancellationToken.None));
    }

    [Fact]
    public async Task Generate_throws_Conflict_when_assigned_attributes_have_no_values()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var (attrId, _) = SeedAttribute("Color"); // no values
        AssignAttribute(productId, attrId);

        await Assert.ThrowsAsync<ConflictException>(() => NewHandler().Handle(
            new GenerateVariantsCommand(productId), CancellationToken.None));
    }

    [Fact]
    public async Task Generate_creates_cartesian_product_of_attribute_values()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var (colorId, _) = SeedAttribute("Color", "Red", "Blue");
        var (sizeId, _) = SeedAttribute("Size", "S", "M");
        AssignAttribute(productId, colorId, 0);
        AssignAttribute(productId, sizeId, 1);

        var dto = await NewHandler().Handle(new GenerateVariantsCommand(productId), CancellationToken.None);

        // 2 colours x 2 sizes = 4 variants, each with 2 attribute-value selections.
        Assert.Equal(4, dto.Variants.Count);
        Assert.All(dto.Variants, v => Assert.Equal(2, v.AttributeValueIds.Count));
    }

    [Fact]
    public async Task Generate_is_idempotent_and_does_not_duplicate_combinations()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var (colorId, _) = SeedAttribute("Color", "Red", "Blue");
        AssignAttribute(productId, colorId);

        await NewHandler().Handle(new GenerateVariantsCommand(productId), CancellationToken.None);
        var second = await NewHandler().Handle(new GenerateVariantsCommand(productId), CancellationToken.None);

        Assert.Equal(2, second.Variants.Count);

        await using var db = NewContext();
        Assert.Equal(2, await db.ProductVariants.CountAsync(v => v.ProductId == productId));
    }

    [Fact]
    public async Task Generate_only_adds_missing_combinations_and_preserves_existing()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var (colorId, colorValues) = SeedAttribute("Color", "Red", "Blue");
        AssignAttribute(productId, colorId);

        // Pre-create the "Red" variant with a custom SKU; generation must keep it and only add "Blue".
        Guid preExistingId;
        await using (var db = NewContext())
        {
            var variant = new Domain.Entities.ProductVariant { ProductId = productId, Sku = "KEEP-RED", DisplayOrder = 5 };
            variant.AttributeValues.Add(new Domain.Entities.ProductVariantAttributeValue { ProductAttributeValueId = colorValues[0] });
            db.ProductVariants.Add(variant);
            await db.SaveChangesAsync();
            preExistingId = variant.Id;
        }

        var dto = await NewHandler().Handle(new GenerateVariantsCommand(productId), CancellationToken.None);

        Assert.Equal(2, dto.Variants.Count);
        Assert.Contains(dto.Variants, v => v.Id == preExistingId && v.Sku == "KEEP-RED");
    }
}
