using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class TierPriceTests : CatalogTestBase
{
    private SetTierPricesCommandHandler NewHandler() => new(NewContext());

    [Fact]
    public async Task SetTierPrices_sets_product_level_tiers()
    {
        var productId = SeedProduct();

        var dto = await NewHandler().Handle(
            new SetTierPricesCommand(productId, null, new()
            {
                new TierPriceInput(1, 10m),
                new TierPriceInput(10, 8m),
            }),
            CancellationToken.None);

        Assert.Equal(2, dto.TierPrices.Count);
        Assert.All(dto.TierPrices, t => Assert.Null(t.ProductVariantId));
    }

    [Fact]
    public async Task SetTierPrices_replaces_existing_product_tiers()
    {
        var productId = SeedProduct();
        await NewHandler().Handle(new SetTierPricesCommand(productId, null, new() { new TierPriceInput(1, 10m) }), CancellationToken.None);

        var dto = await NewHandler().Handle(
            new SetTierPricesCommand(productId, null, new() { new TierPriceInput(5, 7m) }),
            CancellationToken.None);

        Assert.Single(dto.TierPrices);
        Assert.Equal(5, dto.TierPrices[0].MinQuantity);
        Assert.Equal(7m, dto.TierPrices[0].Price);
    }

    [Fact]
    public async Task SetTierPrices_empty_list_clears_tiers()
    {
        var productId = SeedProduct();
        await NewHandler().Handle(new SetTierPricesCommand(productId, null, new() { new TierPriceInput(1, 10m) }), CancellationToken.None);

        var dto = await NewHandler().Handle(new SetTierPricesCommand(productId, null, new()), CancellationToken.None);

        Assert.Empty(dto.TierPrices);
    }

    [Fact]
    public async Task SetTierPrices_sets_variant_level_tiers()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var variantId = SeedVariant(productId);

        var dto = await NewHandler().Handle(
            new SetTierPricesCommand(productId, variantId, new() { new TierPriceInput(2, 9m) }),
            CancellationToken.None);

        Assert.Single(dto.TierPrices);
        Assert.Equal(variantId, dto.TierPrices[0].ProductVariantId);
    }

    [Fact]
    public async Task SetTierPrices_throws_NotFound_when_product_missing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new SetTierPricesCommand(Guid.NewGuid(), null, new()), CancellationToken.None));
    }

    [Fact]
    public async Task SetTierPrices_throws_NotFound_when_variant_not_on_product()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);

        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new SetTierPricesCommand(productId, Guid.NewGuid(), new() { new TierPriceInput(1, 5m) }),
            CancellationToken.None));
    }
}
