using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class ListProductsTests : CatalogTestBase
{
    private ListProductsQueryHandler NewHandler() => new(NewContext());

    [Fact]
    public async Task List_orders_by_display_order_then_name()
    {
        var taxId = SeedTaxCategory();
        SeedProduct(p => { p.Name = "Bravo"; p.DisplayOrder = 1; }, taxId);
        SeedProduct(p => { p.Name = "Alpha"; p.DisplayOrder = 1; }, taxId);
        SeedProduct(p => { p.Name = "Zulu"; p.DisplayOrder = 0; }, taxId);

        var page = await NewHandler().Handle(new ListProductsQuery(), CancellationToken.None);

        Assert.Equal(new[] { "Zulu", "Alpha", "Bravo" }, page.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task List_filters_by_search_term()
    {
        var taxId = SeedTaxCategory();
        SeedProduct(p => p.Name = "Red Shoe", taxCategoryId: taxId);
        SeedProduct(p => p.Name = "Blue Hat", taxCategoryId: taxId);

        var page = await NewHandler().Handle(new ListProductsQuery(Search: "Shoe"), CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("Red Shoe", page.Items[0].Name);
    }

    [Fact]
    public async Task List_filters_by_type_and_published()
    {
        var taxId = SeedTaxCategory();
        SeedProduct(p => { p.Name = "A"; p.ProductType = ProductType.Simple; p.IsPublished = true; }, taxId);
        SeedProduct(p => { p.Name = "B"; p.ProductType = ProductType.GiftCard; p.IsPublished = true; }, taxId);
        SeedProduct(p => { p.Name = "C"; p.ProductType = ProductType.Simple; p.IsPublished = false; }, taxId);

        var byType = await NewHandler().Handle(new ListProductsQuery(Type: ProductType.GiftCard), CancellationToken.None);
        Assert.Single(byType.Items);
        Assert.Equal("B", byType.Items[0].Name);

        var published = await NewHandler().Handle(new ListProductsQuery(IsPublished: false), CancellationToken.None);
        Assert.Single(published.Items);
        Assert.Equal("C", published.Items[0].Name);
    }

    [Fact]
    public async Task List_filters_by_manufacturer()
    {
        var taxId = SeedTaxCategory();
        var manufacturerId = SeedManufacturer();
        SeedProduct(p => { p.Name = "Branded"; p.ManufacturerId = manufacturerId; }, taxId);
        SeedProduct(p => p.Name = "Unbranded", taxCategoryId: taxId);

        var page = await NewHandler().Handle(new ListProductsQuery(ManufacturerId: manufacturerId), CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("Branded", page.Items[0].Name);
    }

    [Fact]
    public async Task List_filters_by_category()
    {
        var taxId = SeedTaxCategory();
        var categoryId = SeedCategory();
        var inCategoryId = SeedProduct(p => p.Name = "InCategory", taxCategoryId: taxId);
        SeedProduct(p => p.Name = "Loose", taxCategoryId: taxId);

        await using (var db = NewContext())
        {
            db.ProductCategories.Add(new ProductCategory { ProductId = inCategoryId, CategoryId = categoryId });
            await db.SaveChangesAsync();
        }

        var page = await NewHandler().Handle(new ListProductsQuery(CategoryId: categoryId), CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("InCategory", page.Items[0].Name);
    }

    [Fact]
    public async Task List_paginates_and_reports_total_count()
    {
        var taxId = SeedTaxCategory();
        for (var i = 0; i < 5; i++)
            SeedProduct(p => { p.Name = $"P{i}"; p.DisplayOrder = i; }, taxId);

        var page = await NewHandler().Handle(new ListProductsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }

    [Fact]
    public async Task List_excludes_soft_deleted_products()
    {
        var taxId = SeedTaxCategory();
        var deletedId = SeedProduct(p => p.Name = "Gone", taxCategoryId: taxId);
        SeedProduct(p => p.Name = "Alive", taxCategoryId: taxId);
        await new DeleteProductCommandHandler(NewContext()).Handle(new DeleteProductCommand(deletedId), CancellationToken.None);

        var page = await NewHandler().Handle(new ListProductsQuery(), CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("Alive", page.Items[0].Name);
    }
}
