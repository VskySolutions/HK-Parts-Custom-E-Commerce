using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Features.ProductAttributes;
using HKPartsEcommerce.Application.Features.StorefrontCatalog;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

/// <summary>
/// Custom-input product attributes: the buyer types the value instead of picking one, so the
/// attribute carries input settings rather than values, and it reaches the storefront through the
/// product's attribute mappings (it drives no variant).
/// </summary>
public class ProductAttributeCustomInputTests : CatalogTestBase
{
    private CreateProductAttributeCommandHandler NewCreateHandler() => new(NewContext());
    private UpdateProductAttributeCommandHandler NewUpdateHandler() => new(NewContext());

    [Fact]
    public async Task Create_custom_input_stores_settings_and_ignores_values()
    {
        var dto = await NewCreateHandler().Handle(
            new CreateProductAttributeCommand(
                "Engraving",
                DisplayType: ProductAttributeDisplayType.CustomInput,
                Values: new() { new ProductAttributeValueInput(null, "Ignored", 0) },
                InputType: ProductAttributeInputType.Number,
                MaxLength: 20,
                IsRequired: true),
            CancellationToken.None);

        Assert.Equal(nameof(ProductAttributeDisplayType.CustomInput), dto.DisplayType);
        Assert.Equal(nameof(ProductAttributeInputType.Number), dto.InputType);
        Assert.Equal(20, dto.MaxLength);
        Assert.True(dto.IsRequired);
        Assert.Empty(dto.Values);

        using var db = NewContext();
        Assert.Empty(db.ProductAttributeValues.Where(v => v.ProductAttributeId == dto.Id));
    }

    [Fact]
    public async Task Create_non_custom_input_drops_the_input_settings()
    {
        var dto = await NewCreateHandler().Handle(
            new CreateProductAttributeCommand(
                "Size",
                DisplayType: ProductAttributeDisplayType.Dropdown,
                Values: new() { new ProductAttributeValueInput(null, "S", 0) },
                InputType: ProductAttributeInputType.Number,
                MaxLength: 20,
                IsRequired: true),
            CancellationToken.None);

        Assert.Equal(nameof(ProductAttributeInputType.Text), dto.InputType);
        Assert.Null(dto.MaxLength);
        Assert.False(dto.IsRequired);
        Assert.Single(dto.Values);
    }

    [Fact]
    public async Task Create_custom_input_treats_a_zero_max_length_as_unlimited()
    {
        var dto = await NewCreateHandler().Handle(
            new CreateProductAttributeCommand(
                "Message",
                DisplayType: ProductAttributeDisplayType.CustomInput,
                MaxLength: 0),
            CancellationToken.None);

        Assert.Null(dto.MaxLength);
    }

    [Fact]
    public async Task Update_to_custom_input_keeps_the_existing_values()
    {
        var (attributeId, _) = SeedAttribute("Size", "S", "M");

        var dto = await NewUpdateHandler().Handle(
            new UpdateProductAttributeCommand(
                attributeId,
                "Size",
                DisplayType: ProductAttributeDisplayType.CustomInput,
                Values: new(),
                MaxLength: 30,
                IsRequired: true),
            CancellationToken.None);

        Assert.Equal(nameof(ProductAttributeDisplayType.CustomInput), dto.DisplayType);
        Assert.Equal(30, dto.MaxLength);
        Assert.True(dto.IsRequired);

        // The values a variant may still reference are left alone, so switching back restores the picker.
        using var db = NewContext();
        Assert.Equal(2, db.ProductAttributeValues.Count(v => v.ProductAttributeId == attributeId));
    }

    [Fact]
    public async Task Update_away_from_custom_input_drops_the_settings_and_reconciles_values()
    {
        var created = await NewCreateHandler().Handle(
            new CreateProductAttributeCommand(
                "Engraving",
                DisplayType: ProductAttributeDisplayType.CustomInput,
                MaxLength: 20,
                IsRequired: true),
            CancellationToken.None);

        var dto = await NewUpdateHandler().Handle(
            new UpdateProductAttributeCommand(
                created.Id,
                "Engraving",
                DisplayType: ProductAttributeDisplayType.Button,
                Values: new() { new ProductAttributeValueInput(null, "Yes", 0) },
                MaxLength: 20,
                IsRequired: true),
            CancellationToken.None);

        Assert.Equal(nameof(ProductAttributeInputType.Text), dto.InputType);
        Assert.Null(dto.MaxLength);
        Assert.False(dto.IsRequired);
        Assert.Single(dto.Values);
    }

    [Fact]
    public void Product_detail_projects_assigned_custom_input_attributes()
    {
        var productId = SeedProduct(p => p.IsPublished = true);
        var attributeId = SeedCustomInputAttribute("Engraving", ProductAttributeInputType.Text, 30, required: true);
        AssignAttribute(productId, attributeId);

        using var db = NewContext();
        var product = db.Products
            .AsNoTracking()
            .Include(p => p.AttributeMappings).ThenInclude(m => m.ProductAttribute)
            .First(p => p.Id == productId);

        var dto = StorefrontProductDetailDto.From(product);

        // It drives no variant, so the mappings are the only route onto the product page.
        var attribute = Assert.Single(dto.Attributes);
        Assert.Equal("Engraving", attribute.Name);
        Assert.Equal(ProductAttributeDisplayType.CustomInput, attribute.DisplayType);
        Assert.Equal(ProductAttributeInputType.Text, attribute.InputType);
        Assert.Equal(30, attribute.MaxLength);
        Assert.True(attribute.IsRequired);
        Assert.Empty(attribute.Values);
    }

    [Fact]
    public void Product_detail_keeps_variant_attributes_and_custom_inputs_in_display_order()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var (colorId, colorValueIds) = SeedAttribute("Colour", "Red");
        SetAttributeDisplayOrder(colorId, 0);
        var engravingId = SeedCustomInputAttribute("Engraving", ProductAttributeInputType.Text, 30, required: false, displayOrder: 1);
        AssignAttribute(productId, colorId, 0);
        AssignAttribute(productId, engravingId, 1);
        SeedVariant(productId, v => v.AttributeValues.Add(new ProductVariantAttributeValue { ProductAttributeValueId = colorValueIds[0] }));

        using var db = NewContext();
        var product = db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues)
                .ThenInclude(av => av.ProductAttributeValue).ThenInclude(pav => pav!.ProductAttribute)
            .Include(p => p.AttributeMappings).ThenInclude(m => m.ProductAttribute)
            .First(p => p.Id == productId);

        var dto = StorefrontProductDetailDto.From(product);

        Assert.Equal(new[] { "Colour", "Engraving" }, dto.Attributes.Select(a => a.Name).ToArray());
        Assert.Single(dto.Attributes[0].Values);
        Assert.Empty(dto.Attributes[1].Values);
    }

    // ---- Seed helpers ---------------------------------------------------------------------------

    private void SetAttributeDisplayOrder(Guid attributeId, int displayOrder)
    {
        using var db = NewContext();
        var attribute = db.ProductAttributes.First(a => a.Id == attributeId);
        attribute.DisplayOrder = displayOrder;
        db.SaveChanges();
    }
}
