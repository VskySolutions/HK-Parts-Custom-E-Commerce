using HKPartsEcommerce.Application.Features.TaxCategories;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.TaxCategories;

/// <summary>
/// Covers the tax-category lookup added so the product form can offer a picker instead of a pasted
/// UUID — the gap that made product creation fail with a missing/invalid TaxCategoryId.
/// </summary>
public class TaxCategoryTests : CatalogTestBase
{
    [Fact]
    public async Task Create_persists_and_returns_dto()
    {
        var dto = await new CreateTaxCategoryCommandHandler(NewContext()).Handle(
            new CreateTaxCategoryCommand("Standard", DefaultRatePercent: 20m), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Standard", dto.Name);
        Assert.Equal(20m, dto.DefaultRatePercent);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task Created_tax_category_is_usable_by_product_create()
    {
        var tax = await new CreateTaxCategoryCommandHandler(NewContext()).Handle(
            new CreateTaxCategoryCommand("Reduced"), CancellationToken.None);

        // The product create handler validates the tax category exists — proves the picker value works.
        var product = await new HKPartsEcommerce.Application.Features.Products.CreateProductCommandHandler(NewContext()).Handle(
            new HKPartsEcommerce.Application.Features.Products.CreateProductCommand(
                "Widget", HKPartsEcommerce.Domain.Enums.ProductType.Simple, tax.Id),
            CancellationToken.None);

        Assert.Equal(tax.Id, product.TaxCategoryId);
    }

    [Fact]
    public void Create_validator_rejects_empty_name()
    {
        var result = new CreateTaxCategoryCommandValidator().Validate(new CreateTaxCategoryCommand(""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_validator_rejects_rate_over_100()
    {
        var result = new CreateTaxCategoryCommandValidator().Validate(
            new CreateTaxCategoryCommand("Std", DefaultRatePercent: 101m));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task List_orders_by_display_order_then_name()
    {
        await using (var db = NewContext())
        {
            db.TaxCategories.Add(new TaxCategory { Name = "Bravo", DisplayOrder = 1 });
            db.TaxCategories.Add(new TaxCategory { Name = "Alpha", DisplayOrder = 1 });
            db.TaxCategories.Add(new TaxCategory { Name = "Zulu", DisplayOrder = 0 });
            await db.SaveChangesAsync();
        }

        var page = await new ListTaxCategoriesQueryHandler(NewContext()).Handle(
            new ListTaxCategoriesQuery(), CancellationToken.None);

        Assert.Equal(new[] { "Zulu", "Alpha", "Bravo" }, page.Items.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task List_active_only_excludes_inactive()
    {
        await using (var db = NewContext())
        {
            db.TaxCategories.Add(new TaxCategory { Name = "Active", IsActive = true });
            db.TaxCategories.Add(new TaxCategory { Name = "Retired", IsActive = false });
            await db.SaveChangesAsync();
        }

        var page = await new ListTaxCategoriesQueryHandler(NewContext()).Handle(
            new ListTaxCategoriesQuery(ActiveOnly: true), CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("Active", page.Items[0].Name);
    }
}
