using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class VariantTests : CatalogTestBase
{
    [Fact]
    public async Task UpdateVariant_changes_purchasable_config()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var variantId = SeedVariant(productId);

        var dto = await new UpdateVariantCommandHandler(NewContext()).Handle(
            new UpdateVariantCommand(variantId, Sku: "V-9", Price: 12.5m, StockQuantity: 4, AllowBackorder: true, IsEnabled: false, DisplayOrder: 2),
            CancellationToken.None);

        Assert.Equal("V-9", dto.Sku);
        Assert.Equal(12.5m, dto.Price);
        Assert.False(dto.IsEnabled);

        await using var db = NewContext();
        var persisted = await db.ProductVariants.SingleAsync(v => v.Id == variantId);
        // Stock is per store now (InventoryLevels); UpdateVariant no longer persists a catalog stock number.
        Assert.Equal(0, persisted.StockQuantity);
        Assert.True(persisted.AllowBackorder);
    }

    [Fact]
    public async Task UpdateVariant_throws_NotFound_when_missing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateVariantCommandHandler(NewContext()).Handle(
            new UpdateVariantCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteVariant_soft_deletes()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var variantId = SeedVariant(productId);

        await new DeleteVariantCommandHandler(NewContext()).Handle(new DeleteVariantCommand(variantId), CancellationToken.None);

        await using var db = NewContext();
        Assert.False(await db.ProductVariants.AnyAsync(v => v.Id == variantId));
        var row = await db.ProductVariants.IgnoreQueryFilters().SingleAsync(v => v.Id == variantId);
        Assert.True(row.Deleted);
    }

    [Fact]
    public async Task DeleteVariant_is_idempotent_for_missing_id()
    {
        await new DeleteVariantCommandHandler(NewContext()).Handle(new DeleteVariantCommand(Guid.NewGuid()), CancellationToken.None);
    }
}
