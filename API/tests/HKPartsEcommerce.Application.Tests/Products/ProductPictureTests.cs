using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

/// <summary>
/// Covers the unified Media-backed product gallery (WO-123 pattern): assigning image assets,
/// adding video embeds, listing and removing pictures. Replaces the legacy ProductImage CRUD.
/// </summary>
public class ProductPictureTests : CatalogTestBase
{
    // ---- Assign (image) ---------------------------------------------------------------------------

    [Fact]
    public async Task Assign_creates_product_level_picture()
    {
        var productId = SeedProduct();
        var mediaId = SeedMedia("https://cdn/a.png");

        var dto = await new AssignProductPictureCommandHandler(NewContext()).Handle(
            new AssignProductPictureCommand(productId, mediaId, DisplayOrder: 1),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Null(dto.ProductVariantId);
        Assert.Equal(mediaId, dto.MediaId);
        Assert.Equal("https://cdn/a.png", dto.Url);

        await using var db = NewContext();
        Assert.Equal(1, await db.ProductPictures.CountAsync(i => i.ProductId == productId));
    }

    [Fact]
    public async Task Assign_creates_variant_level_picture()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var variantId = SeedVariant(productId);
        var mediaId = SeedMedia("https://cdn/v.png");

        var dto = await new AssignProductPictureCommandHandler(NewContext()).Handle(
            new AssignProductPictureCommand(productId, mediaId, ProductVariantId: variantId),
            CancellationToken.None);

        Assert.Equal(variantId, dto.ProductVariantId);
    }

    [Fact]
    public async Task Assign_throws_NotFound_when_product_missing()
    {
        var mediaId = SeedMedia();

        await Assert.ThrowsAsync<NotFoundException>(() => new AssignProductPictureCommandHandler(NewContext()).Handle(
            new AssignProductPictureCommand(Guid.NewGuid(), mediaId),
            CancellationToken.None));
    }

    [Fact]
    public async Task Assign_throws_NotFound_when_media_missing()
    {
        var productId = SeedProduct();

        await Assert.ThrowsAsync<NotFoundException>(() => new AssignProductPictureCommandHandler(NewContext()).Handle(
            new AssignProductPictureCommand(productId, Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task Assign_throws_NotFound_when_variant_belongs_to_other_product()
    {
        var productId = SeedProduct();
        var otherProductId = SeedProduct(p => p.ProductType = ProductType.WithVariants);
        var foreignVariantId = SeedVariant(otherProductId);
        var mediaId = SeedMedia();

        await Assert.ThrowsAsync<NotFoundException>(() => new AssignProductPictureCommandHandler(NewContext()).Handle(
            new AssignProductPictureCommand(productId, mediaId, ProductVariantId: foreignVariantId),
            CancellationToken.None));
    }

    // ---- Add video --------------------------------------------------------------------------------

    [Fact]
    public async Task AddVideo_creates_video_media_and_picture()
    {
        var productId = SeedProduct();

        var dto = await new AddProductVideoCommandHandler(NewContext()).Handle(
            new AddProductVideoCommand(productId, "https://youtu.be/x", AltText: "clip"),
            CancellationToken.None);

        Assert.Equal(MediaType.Video, dto.MediaType);
        Assert.Equal("https://youtu.be/x", dto.Url);

        await using var db = NewContext();
        Assert.Equal(1, await db.ProductPictures.CountAsync(i => i.ProductId == productId));
        Assert.True(await db.Media.AnyAsync(m => m.Id == dto.MediaId && m.MediaType == MediaType.Video));
    }

    [Fact]
    public async Task AddVideo_throws_NotFound_when_product_missing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new AddProductVideoCommandHandler(NewContext()).Handle(
            new AddProductVideoCommand(Guid.NewGuid(), "https://youtu.be/x"),
            CancellationToken.None));
    }

    // ---- List -------------------------------------------------------------------------------------

    [Fact]
    public async Task List_returns_pictures_in_display_order()
    {
        var productId = SeedProduct();
        SeedPicture(productId, url: "https://cdn/2.png", displayOrder: 1);
        SeedPicture(productId, url: "https://cdn/1.png", displayOrder: 0);

        var result = await new ListProductPicturesQueryHandler(NewContext()).Handle(
            new ListProductPicturesQuery(productId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("https://cdn/1.png", result[0].Url);
        Assert.Equal("https://cdn/2.png", result[1].Url);
    }

    // ---- Remove -----------------------------------------------------------------------------------

    [Fact]
    public async Task Remove_deletes_picture_but_keeps_media()
    {
        var productId = SeedProduct();
        var mediaId = SeedMedia("https://cdn/keep.png");
        Guid pictureId;
        await using (var db = NewContext())
        {
            var picture = new Domain.Entities.ProductPicture { ProductId = productId, MediaId = mediaId };
            db.ProductPictures.Add(picture);
            await db.SaveChangesAsync();
            pictureId = picture.Id;
        }

        await new RemoveProductPictureCommandHandler(NewContext()).Handle(
            new RemoveProductPictureCommand(pictureId), CancellationToken.None);

        await using var check = NewContext();
        Assert.False(await check.ProductPictures.AnyAsync(i => i.Id == pictureId));
        Assert.True(await check.Media.AnyAsync(m => m.Id == mediaId)); // underlying asset untouched
    }

    [Fact]
    public async Task Remove_is_idempotent_for_missing_id()
    {
        await new RemoveProductPictureCommandHandler(NewContext()).Handle(
            new RemoveProductPictureCommand(Guid.NewGuid()), CancellationToken.None);
    }
}
