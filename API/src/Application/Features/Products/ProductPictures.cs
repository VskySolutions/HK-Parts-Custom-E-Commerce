using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>
/// A product picture: a Media asset (image or video embed) assigned to a product or one of its
/// variants, with the asset's public URL read directly from the Media row (WO-123; unified media).
/// </summary>
public class ProductPictureDto
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public MediaType MediaType { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }

    public static ProductPictureDto From(ProductPicture p) => new()
    {
        Id = p.Id,
        MediaId = p.MediaId,
        ProductVariantId = p.ProductVariantId,
        MediaType = p.Media?.MediaType ?? MediaType.Image,
        Url = p.Media?.Url ?? string.Empty,
        AltText = p.Media?.AltText,
        DisplayOrder = p.DisplayOrder,
    };
}

// ---- List --------------------------------------------------------------------

/// <summary>Lists a product's Media-backed pictures (images + videos, ordered), each with its public URL.</summary>
public record ListProductPicturesQuery(Guid ProductId) : IRequest<List<ProductPictureDto>>;

public class ListProductPicturesQueryHandler : IRequestHandler<ListProductPicturesQuery, List<ProductPictureDto>>
{
    private readonly IApplicationDbContext _db;

    public ListProductPicturesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<ProductPictureDto>> Handle(ListProductPicturesQuery request, CancellationToken cancellationToken)
    {
        var rows = await _db.ProductPictures.AsNoTracking()
            .Where(p => p.ProductId == request.ProductId)
            .Include(p => p.Media)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        return rows.Select(ProductPictureDto.From).ToList();
    }
}

// ---- Assign (image) ----------------------------------------------------------

/// <summary>Assigns a committed Media asset to a product (optionally scoped to a variant) as a picture.</summary>
public record AssignProductPictureCommand(Guid ProductId, Guid MediaId, Guid? ProductVariantId = null, int DisplayOrder = 0)
    : IRequest<ProductPictureDto>;

public class AssignProductPictureCommandValidator : AbstractValidator<AssignProductPictureCommand>
{
    public AssignProductPictureCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.MediaId).NotEmpty();
    }
}

public class AssignProductPictureCommandHandler : IRequestHandler<AssignProductPictureCommand, ProductPictureDto>
{
    private readonly IApplicationDbContext _db;

    public AssignProductPictureCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductPictureDto> Handle(AssignProductPictureCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
            throw new NotFoundException(nameof(Product), request.ProductId);

        if (request.ProductVariantId is Guid variantId
            && !await _db.ProductVariants.AnyAsync(v => v.Id == variantId && v.ProductId == request.ProductId, cancellationToken))
            throw new NotFoundException(nameof(ProductVariant), variantId);

        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Media), request.MediaId);

        var order = request.DisplayOrder;
        if (order == 0)
        {
            var max = await _db.ProductPictures.Where(p => p.ProductId == request.ProductId)
                .Select(p => (int?)p.DisplayOrder).MaxAsync(cancellationToken) ?? -1;
            order = max + 1;
        }

        var picture = new ProductPicture
        {
            ProductId = request.ProductId,
            ProductVariantId = request.ProductVariantId,
            MediaId = request.MediaId,
            DisplayOrder = order,
        };
        _db.ProductPictures.Add(picture);
        await _db.SaveChangesAsync(cancellationToken);

        picture.Media = media;
        return ProductPictureDto.From(picture);
    }
}

// ---- Add video (embed URL) ---------------------------------------------------

/// <summary>
/// Adds a video embed (YouTube/Vimeo/…) to a product as a Media-backed picture: creates a video
/// Media row holding the embed URL, then assigns it. Keeps all product media in the one pattern.
/// </summary>
public record AddProductVideoCommand(Guid ProductId, string Url, string? AltText = null, Guid? ProductVariantId = null)
    : IRequest<ProductPictureDto>;

public class AddProductVideoCommandValidator : AbstractValidator<AddProductVideoCommand>
{
    public AddProductVideoCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.AltText).MaximumLength(500);
    }
}

public class AddProductVideoCommandHandler : IRequestHandler<AddProductVideoCommand, ProductPictureDto>
{
    private readonly IApplicationDbContext _db;

    public AddProductVideoCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductPictureDto> Handle(AddProductVideoCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
            throw new NotFoundException(nameof(Product), request.ProductId);

        if (request.ProductVariantId is Guid variantId
            && !await _db.ProductVariants.AnyAsync(v => v.Id == variantId && v.ProductId == request.ProductId, cancellationToken))
            throw new NotFoundException(nameof(ProductVariant), variantId);

        var url = request.Url.Trim();

        // A video embed is a URL, not an uploaded file: the embed URL is both the asset key and the
        // resolved URL. SeoFileName only needs to be unique among live media.
        var media = new Domain.Entities.Media
        {
            OriginalFileName = url.Length > 400 ? url[..400] : url,
            SeoFileName = "video-" + Guid.NewGuid().ToString("n"),
            AssetKey = url,
            Url = url,
            MediaType = MediaType.Video,
            MimeType = "text/html",
            FileSizeBytes = 0,
            AltText = request.AltText,
        };
        _db.Media.Add(media);

        var max = await _db.ProductPictures.Where(p => p.ProductId == request.ProductId)
            .Select(p => (int?)p.DisplayOrder).MaxAsync(cancellationToken) ?? -1;

        var picture = new ProductPicture
        {
            ProductId = request.ProductId,
            ProductVariantId = request.ProductVariantId,
            MediaId = media.Id,
            DisplayOrder = max + 1,
        };
        _db.ProductPictures.Add(picture);
        await _db.SaveChangesAsync(cancellationToken);

        picture.Media = media;
        return ProductPictureDto.From(picture);
    }
}

// ---- Remove ------------------------------------------------------------------

/// <summary>Removes a product picture (the underlying Media asset is left intact).</summary>
public record RemoveProductPictureCommand(Guid PictureId) : IRequest;

public class RemoveProductPictureCommandHandler : IRequestHandler<RemoveProductPictureCommand>
{
    private readonly IApplicationDbContext _db;

    public RemoveProductPictureCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RemoveProductPictureCommand request, CancellationToken cancellationToken)
    {
        var picture = await _db.ProductPictures.FirstOrDefaultAsync(p => p.Id == request.PictureId, cancellationToken);
        if (picture is null)
            return;

        _db.ProductPictures.Remove(picture);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
