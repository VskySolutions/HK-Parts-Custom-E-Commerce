using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class UpdateProductTests : CatalogTestBase
{
    private UpdateProductCommandHandler NewHandler() => new(NewContext());

    [Fact]
    public async Task Update_changes_scalar_fields()
    {
        var taxId = SeedTaxCategory();
        var productId = SeedProduct(p => { p.Name = "Old"; p.Price = 1m; }, taxId);

        var dto = await NewHandler().Handle(
            new UpdateProductCommand(productId, "New", ProductType.Simple, taxId, Sku: "S-9", Price: 42m, StockQuantity: 7, IsPublished: true),
            CancellationToken.None);

        Assert.Equal("New", dto.Name);
        Assert.Equal(42m, dto.Price);
        Assert.True(dto.IsPublished);

        await using var db = NewContext();
        var persisted = await db.Products.SingleAsync(p => p.Id == productId);
        Assert.Equal("New", persisted.Name);
        // Stock is per store now (InventoryLevels); update no longer persists a catalog stock number.
        Assert.Equal(0, persisted.StockQuantity);
    }

    [Fact]
    public async Task Update_can_change_product_type()
    {
        var taxId = SeedTaxCategory();
        var productId = SeedProduct(p => p.ProductType = ProductType.Simple, taxId);

        var dto = await NewHandler().Handle(
            new UpdateProductCommand(productId, "P", ProductType.WithVariants, taxId),
            CancellationToken.None);

        Assert.Equal(ProductType.WithVariants, dto.ProductType);
    }

    [Fact]
    public async Task Update_throws_NotFound_when_product_missing()
    {
        var taxId = SeedTaxCategory();

        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new UpdateProductCommand(Guid.NewGuid(), "P", ProductType.Simple, taxId),
            CancellationToken.None));
    }

    [Fact]
    public async Task Update_throws_NotFound_when_tax_category_missing()
    {
        var productId = SeedProduct();

        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new UpdateProductCommand(productId, "P", ProductType.Simple, Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task Update_throws_NotFound_when_manufacturer_missing()
    {
        var taxId = SeedTaxCategory();
        var productId = SeedProduct(taxCategoryId: taxId);

        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new UpdateProductCommand(productId, "P", ProductType.Simple, taxId, ManufacturerId: Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task Update_bumps_audit_updated_timestamp()
    {
        var taxId = SeedTaxCategory();
        var productId = SeedProduct(taxCategoryId: taxId);

        DateTime createdUpdatedOn;
        await using (var db = NewContext())
            createdUpdatedOn = (await db.Products.SingleAsync(p => p.Id == productId)).UpdatedOnUtc;

        await NewHandler().Handle(
            new UpdateProductCommand(productId, "Renamed", ProductType.Simple, taxId),
            CancellationToken.None);

        await using var db2 = NewContext();
        var persisted = await db2.Products.SingleAsync(p => p.Id == productId);
        Assert.True(persisted.UpdatedOnUtc >= createdUpdatedOn);
    }
}
