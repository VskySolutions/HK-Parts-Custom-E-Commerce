using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

public class DeleteProductTests : CatalogTestBase
{
    private DeleteProductCommandHandler NewHandler() => new(NewContext());

    [Fact]
    public async Task Delete_soft_deletes_and_hides_from_filtered_queries()
    {
        var productId = SeedProduct();

        await NewHandler().Handle(new DeleteProductCommand(productId), CancellationToken.None);

        await using var db = NewContext();
        // Excluded from normal (query-filtered) reads.
        Assert.False(await db.Products.AnyAsync(p => p.Id == productId));
        // Row physically retained with the soft-delete flag set.
        var row = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == productId);
        Assert.True(row.Deleted);
        Assert.NotNull(row.DeletedOnUtc);
    }

    [Fact]
    public async Task Delete_is_idempotent_for_missing_id()
    {
        // Should not throw when the product does not exist.
        await NewHandler().Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None);
    }

    [Fact]
    public async Task Delete_is_idempotent_when_already_deleted()
    {
        var productId = SeedProduct();
        await NewHandler().Handle(new DeleteProductCommand(productId), CancellationToken.None);

        // Second delete finds nothing through the filter and returns quietly.
        await NewHandler().Handle(new DeleteProductCommand(productId), CancellationToken.None);

        await using var db = NewContext();
        Assert.Equal(1, await db.Products.IgnoreQueryFilters().CountAsync(p => p.Id == productId));
    }
}
