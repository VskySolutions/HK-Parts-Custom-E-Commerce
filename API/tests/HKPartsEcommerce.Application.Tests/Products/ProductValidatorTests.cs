using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

/// <summary>Pure FluentValidation rule tests (no database) for the product command validators.</summary>
public class ProductValidatorTests
{
    // ---- CreateProduct ----------------------------------------------------------------------------

    [Fact]
    public void CreateProduct_valid_command_passes()
    {
        var result = new CreateProductCommandValidator().Validate(
            new CreateProductCommand("Widget", ProductType.Simple, Guid.NewGuid(), Slug: "widget", Sku: "W1"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateProduct_empty_name_fails()
    {
        var result = new CreateProductCommandValidator().Validate(
            new CreateProductCommand("", ProductType.Simple, Guid.NewGuid()));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProductCommand.Name));
    }

    [Fact]
    public void CreateProduct_name_over_400_chars_fails()
    {
        var result = new CreateProductCommandValidator().Validate(
            new CreateProductCommand(new string('x', 401), ProductType.Simple, Guid.NewGuid()));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProductCommand.Name));
    }

    [Fact]
    public void CreateProduct_empty_tax_category_fails()
    {
        var result = new CreateProductCommandValidator().Validate(
            new CreateProductCommand("Widget", ProductType.Simple, Guid.Empty));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProductCommand.TaxCategoryId));
    }

    [Fact]
    public void CreateProduct_slug_and_sku_over_400_chars_fail()
    {
        var result = new CreateProductCommandValidator().Validate(
            new CreateProductCommand("Widget", ProductType.Simple, Guid.NewGuid(), Slug: new string('s', 401), Sku: new string('k', 401)));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProductCommand.Slug));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProductCommand.Sku));
    }

    // ---- UpdateProduct ----------------------------------------------------------------------------

    [Fact]
    public void UpdateProduct_empty_id_fails()
    {
        var result = new UpdateProductCommandValidator().Validate(
            new UpdateProductCommand(Guid.Empty, "Widget", ProductType.Simple, Guid.NewGuid()));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProductCommand.Id));
    }

    [Fact]
    public void UpdateProduct_valid_command_passes()
    {
        var result = new UpdateProductCommandValidator().Validate(
            new UpdateProductCommand(Guid.NewGuid(), "Widget", ProductType.Simple, Guid.NewGuid()));
        Assert.True(result.IsValid);
    }

    // ---- UpdateVariant ----------------------------------------------------------------------------

    [Fact]
    public void UpdateVariant_empty_id_fails()
    {
        var result = new UpdateVariantCommandValidator().Validate(new UpdateVariantCommand(Guid.Empty));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateVariantCommand.VariantId));
    }

    [Fact]
    public void UpdateVariant_sku_over_400_chars_fails()
    {
        var result = new UpdateVariantCommandValidator().Validate(
            new UpdateVariantCommand(Guid.NewGuid(), Sku: new string('s', 401)));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateVariantCommand.Sku));
    }

    // ---- AssignProductPicture ---------------------------------------------------------------------

    [Fact]
    public void AssignProductPicture_empty_product_or_media_fails()
    {
        var result = new AssignProductPictureCommandValidator().Validate(
            new AssignProductPictureCommand(Guid.Empty, Guid.Empty));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AssignProductPictureCommand.ProductId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AssignProductPictureCommand.MediaId));
    }

    [Fact]
    public void AssignProductPicture_valid_command_passes()
    {
        var result = new AssignProductPictureCommandValidator().Validate(
            new AssignProductPictureCommand(Guid.NewGuid(), Guid.NewGuid()));
        Assert.True(result.IsValid);
    }

    // ---- SetTierPrices ----------------------------------------------------------------------------

    [Fact]
    public void SetTierPrices_non_positive_quantity_fails()
    {
        var result = new SetTierPricesCommandValidator().Validate(
            new SetTierPricesCommand(Guid.NewGuid(), null, new() { new TierPriceInput(0, 5m) }));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void SetTierPrices_negative_price_fails()
    {
        var result = new SetTierPricesCommandValidator().Validate(
            new SetTierPricesCommand(Guid.NewGuid(), null, new() { new TierPriceInput(1, -1m) }));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void SetTierPrices_valid_command_passes()
    {
        var result = new SetTierPricesCommandValidator().Validate(
            new SetTierPricesCommand(Guid.NewGuid(), null, new() { new TierPriceInput(1, 5m), new TierPriceInput(10, 4m) }));
        Assert.True(result.IsValid);
    }

    // ---- AddProductVideo --------------------------------------------------------------------------

    [Fact]
    public void AddProductVideo_empty_product_or_url_fails()
    {
        var result = new AddProductVideoCommandValidator().Validate(
            new AddProductVideoCommand(Guid.Empty, ""));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddProductVideoCommand.ProductId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddProductVideoCommand.Url));
    }

    [Fact]
    public void AddProductVideo_valid_command_passes()
    {
        var result = new AddProductVideoCommandValidator().Validate(
            new AddProductVideoCommand(Guid.NewGuid(), "https://youtu.be/x"));
        Assert.True(result.IsValid);
    }
}
