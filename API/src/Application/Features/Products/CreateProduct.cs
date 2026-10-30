using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Creates a catalog product of any type. A Tax Category is mandatory (AC-CAT-001.6).</summary>
public record CreateProductCommand(
    string Name,
    ProductType ProductType,
    Guid TaxCategoryId,
    string? Slug = null,
    string? ShortDescription = null,
    string? FullDescription = null,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? MetaKeywords = null,
    string? Sku = null,
    decimal? Price = null,
    int StockQuantity = 0,
    bool AllowBackorder = false,
    DateTime? EstimatedRestockDate = null,
    Guid? ManufacturerId = null,
    bool IsPublished = false,
    bool ReviewsEnabled = true,
    bool IsInquiryOnly = false,
    string? InquiryButtonLabel = null,
    int DisplayOrder = 0,
    bool IsFeatured = false,
    int FeaturedDisplayOrder = 0,
    int? DownloadExpiryDays = null,
    int? DownloadLimit = null,
    GiftCardType? GiftCardType = null,
    decimal? GiftCardAmount = null,
    string? H1 = null,
    Guid? MetaImageMediaId = null,
    string? MetaOpenGraph = null,
    string? MetaCustomTags = null,
    string? MetaFooterTags = null,
    string? Gtin = null,
    decimal? Cost = null,
    decimal? Msrp = null,
    decimal? SalePrice = null,
    DateTime? SaleStartDateUtc = null,
    DateTime? SaleEndDateUtc = null,
    string? TaxCode = null,
    bool IsPurchasable = true,
    bool IsFflRequired = false,
    int? MinOrderQuantity = null,
    int? MaxOrderQuantity = null,
    StockStatus? StockStatusOverride = null,
    string? BackorderMessage = null,
    decimal? Weight = null,
    decimal? Length = null,
    decimal? Width = null,
    decimal? Height = null,
    int PackagePieces = 1,
    string? PackageType = null,
    string? FreightClass = null,
    bool IsHazardous = false) : IRequest<ProductDto>;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(400);
        RuleFor(x => x.TaxCategoryId).NotEmpty();
        RuleFor(x => x.Slug).MaximumLength(400);
        RuleFor(x => x.Sku).MaximumLength(400);
        RuleFor(x => x.MetaTitle).MaximumLength(300);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
        RuleFor(x => x.MetaKeywords).MaximumLength(500);
        RuleFor(x => x.H1).MaximumLength(300);
        RuleFor(x => x.Gtin).MaximumLength(50);
        RuleFor(x => x.TaxCode).MaximumLength(50);
        RuleFor(x => x.BackorderMessage).MaximumLength(500);
        RuleFor(x => x.PackageType).MaximumLength(50);
        RuleFor(x => x.FreightClass).MaximumLength(10);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
        RuleFor(x => x.Msrp).GreaterThanOrEqualTo(0).When(x => x.Msrp.HasValue);
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0).When(x => x.SalePrice.HasValue);
        RuleFor(x => x.SaleEndDateUtc).GreaterThanOrEqualTo(x => x.SaleStartDateUtc!.Value)
            .When(x => x.SaleStartDateUtc.HasValue && x.SaleEndDateUtc.HasValue)
            .WithMessage("Sale end date must be on or after the sale start date.");
        RuleFor(x => x.MinOrderQuantity).GreaterThanOrEqualTo(1).When(x => x.MinOrderQuantity.HasValue);
        RuleFor(x => x.MaxOrderQuantity).GreaterThanOrEqualTo(1).When(x => x.MaxOrderQuantity.HasValue);
        RuleFor(x => x.MaxOrderQuantity).GreaterThanOrEqualTo(x => x.MinOrderQuantity!.Value)
            .When(x => x.MinOrderQuantity.HasValue && x.MaxOrderQuantity.HasValue)
            .WithMessage("Max order quantity must be at least the min order quantity.");
        RuleFor(x => x.Weight).GreaterThanOrEqualTo(0).When(x => x.Weight.HasValue);
        RuleFor(x => x.Length).GreaterThanOrEqualTo(0).When(x => x.Length.HasValue);
        RuleFor(x => x.Width).GreaterThanOrEqualTo(0).When(x => x.Width.HasValue);
        RuleFor(x => x.Height).GreaterThanOrEqualTo(0).When(x => x.Height.HasValue);
        RuleFor(x => x.PackagePieces).GreaterThanOrEqualTo(1);
    }
}

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public CreateProductCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.TaxCategories.AnyAsync(t => t.Id == request.TaxCategoryId, cancellationToken))
            throw new NotFoundException(nameof(TaxCategory), request.TaxCategoryId);

        if (request.ManufacturerId is Guid manufacturerId
            && !await _db.Manufacturers.AnyAsync(m => m.Id == manufacturerId, cancellationToken))
            throw new NotFoundException(nameof(Manufacturer), manufacturerId);

        if (request.MetaImageMediaId is Guid metaImageId
            && !await _db.Media.AnyAsync(m => m.Id == metaImageId, cancellationToken))
            throw new NotFoundException(nameof(Media), metaImageId);

        var entity = new Product
        {
            Name = request.Name,
            Slug = request.Slug,
            ProductType = request.ProductType,
            ShortDescription = request.ShortDescription,
            FullDescription = request.FullDescription,
            MetaTitle = request.MetaTitle,
            MetaDescription = request.MetaDescription,
            MetaKeywords = request.MetaKeywords,
            Sku = request.Sku,
            Price = request.Price,
            AllowBackorder = request.AllowBackorder,
            EstimatedRestockDate = request.EstimatedRestockDate,
            TaxCategoryId = request.TaxCategoryId,
            ManufacturerId = request.ManufacturerId,
            IsPublished = request.IsPublished,
            ReviewsEnabled = request.ReviewsEnabled,
            IsInquiryOnly = request.IsInquiryOnly,
            InquiryButtonLabel = request.InquiryButtonLabel,
            DisplayOrder = request.DisplayOrder,
            IsFeatured = request.IsFeatured,
            FeaturedDisplayOrder = request.FeaturedDisplayOrder,
            DownloadExpiryDays = request.DownloadExpiryDays,
            DownloadLimit = request.DownloadLimit,
            GiftCardType = request.GiftCardType,
            GiftCardAmount = request.GiftCardAmount,
            H1 = request.H1,
            MetaImageMediaId = request.MetaImageMediaId,
            MetaOpenGraph = request.MetaOpenGraph,
            MetaCustomTags = request.MetaCustomTags,
            MetaFooterTags = request.MetaFooterTags,
            Gtin = request.Gtin,
            Cost = request.Cost,
            Msrp = request.Msrp,
            SalePrice = request.SalePrice,
            SaleStartDateUtc = request.SaleStartDateUtc,
            SaleEndDateUtc = request.SaleEndDateUtc,
            TaxCode = request.TaxCode,
            IsPurchasable = request.IsPurchasable,
            IsFflRequired = request.IsFflRequired,
            MinOrderQuantity = request.MinOrderQuantity,
            MaxOrderQuantity = request.MaxOrderQuantity,
            StockStatusOverride = request.StockStatusOverride,
            BackorderMessage = request.BackorderMessage,
            Weight = request.Weight,
            Length = request.Length,
            Width = request.Width,
            Height = request.Height,
            PackagePieces = request.PackagePieces,
            PackageType = request.PackageType,
            FreightClass = request.FreightClass,
            IsHazardous = request.IsHazardous,
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(entity);
    }
}
