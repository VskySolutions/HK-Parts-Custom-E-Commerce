using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class CreateProductTests : CatalogTestBase
{
    private CreateProductCommandHandler NewHandler() => new(NewContext());

    [Fact]
    public async Task Create_persists_product_and_returns_dto()
    {
        var taxId = SeedTaxCategory();

        var dto = await NewHandler().Handle(
            new CreateProductCommand("Widget", ProductType.Simple, taxId, Sku: "W-1", Price: 9.99m, StockQuantity: 5),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Widget", dto.Name);
        Assert.Equal("W-1", dto.Sku);
        Assert.Equal(9.99m, dto.Price);

        await using var db = NewContext();
        var persisted = await db.Products.SingleAsync(p => p.Id == dto.Id);
        Assert.Equal("Widget", persisted.Name);
        // Stock is now held per store in InventoryLevels; the create command no longer persists a catalog
        // stock number, so the rollup (no inventory rows) is 0.
        Assert.Equal(0, persisted.StockQuantity);
    }

    [Fact]
    public async Task Create_stamps_audit_created_timestamp()
    {
        var taxId = SeedTaxCategory();

        var dto = await NewHandler().Handle(
            new CreateProductCommand("Widget", ProductType.Simple, taxId),
            CancellationToken.None);

        await using var db = NewContext();
        var persisted = await db.Products.SingleAsync(p => p.Id == dto.Id);
        Assert.NotEqual(default, persisted.CreatedOnUtc);
        Assert.NotEqual(default, persisted.UpdatedOnUtc);
    }

    [Fact]
    public async Task Create_throws_NotFound_when_tax_category_missing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new CreateProductCommand("Widget", ProductType.Simple, Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task Create_throws_NotFound_when_manufacturer_missing()
    {
        var taxId = SeedTaxCategory();

        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new CreateProductCommand("Widget", ProductType.Simple, taxId, ManufacturerId: Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task Create_allows_valid_manufacturer()
    {
        var taxId = SeedTaxCategory();
        var manufacturerId = SeedManufacturer();

        var dto = await NewHandler().Handle(
            new CreateProductCommand("Widget", ProductType.Simple, taxId, ManufacturerId: manufacturerId),
            CancellationToken.None);

        Assert.Equal(manufacturerId, dto.ManufacturerId);
    }

    [Fact]
    public async Task Create_persists_downloadable_specific_fields()
    {
        var taxId = SeedTaxCategory();

        var dto = await NewHandler().Handle(
            new CreateProductCommand("Ebook", ProductType.Downloadable, taxId, DownloadExpiryDays: 30, DownloadLimit: 3),
            CancellationToken.None);

        Assert.Equal(ProductType.Downloadable, dto.ProductType);
        Assert.Equal(30, dto.DownloadExpiryDays);
        Assert.Equal(3, dto.DownloadLimit);
    }

    [Fact]
    public async Task Create_persists_gift_card_specific_fields()
    {
        var taxId = SeedTaxCategory();

        var dto = await NewHandler().Handle(
            new CreateProductCommand("GC", ProductType.GiftCard, taxId, GiftCardType: GiftCardType.Fixed, GiftCardAmount: 25m),
            CancellationToken.None);

        Assert.Equal(GiftCardType.Fixed, dto.GiftCardType);
        Assert.Equal(25m, dto.GiftCardAmount);
    }
}
