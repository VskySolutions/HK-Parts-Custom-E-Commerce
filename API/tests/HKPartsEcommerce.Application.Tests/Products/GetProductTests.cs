using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class GetProductTests : CatalogTestBase
{
    private GetProductQueryHandler NewHandler() => new(NewContext());

    [Fact]
    public async Task Get_returns_product_with_child_graph()
    {
        var taxId = SeedTaxCategory();
        var productId = SeedProduct(p => { p.Name = "Graphed"; p.ProductType = ProductType.WithVariants; }, taxId);
        var variantId = SeedVariant(productId, v => v.Sku = "V-1");
        SeedPicture(productId);

        var dto = await NewHandler().Handle(new GetProductQuery(productId), CancellationToken.None);

        Assert.Equal("Graphed", dto.Name);
        Assert.Single(dto.Variants);
        Assert.Equal(variantId, dto.Variants[0].Id);
        Assert.Single(dto.Pictures);
    }

    [Fact]
    public async Task Get_throws_NotFound_when_missing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new GetProductQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Get_throws_NotFound_for_soft_deleted_product()
    {
        var productId = SeedProduct();
        await new DeleteProductCommandHandler(NewContext()).Handle(new DeleteProductCommand(productId), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => NewHandler().Handle(
            new GetProductQuery(productId), CancellationToken.None));
    }
}
