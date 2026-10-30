using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Updates a product's scalar configuration (the route id wins over any id in the body).</summary>
public record UpdateProductCommand(
    Guid Id,
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

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateProductCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        if (!await _db.TaxCategories.AnyAsync(t => t.Id == request.TaxCategoryId, cancellationToken))
            throw new NotFoundException(nameof(TaxCategory), request.TaxCategoryId);

        if (request.ManufacturerId is Guid manufacturerId
            && !await _db.Manufacturers.AnyAsync(m => m.Id == manufacturerId, cancellationToken))
            throw new NotFoundException(nameof(Manufacturer), manufacturerId);

        if (request.MetaImageMediaId is Guid metaImageId
            && !await _db.Media.AnyAsync(m => m.Id == metaImageId, cancellationToken))
            throw new NotFoundException(nameof(Media), metaImageId);

        entity.Name = request.Name;
        entity.Slug = request.Slug;
        entity.ProductType = request.ProductType;
        entity.ShortDescription = request.ShortDescription;
        entity.FullDescription = request.FullDescription;
        entity.MetaTitle = request.MetaTitle;
        entity.MetaDescription = request.MetaDescription;
        entity.MetaKeywords = request.MetaKeywords;
        entity.Sku = request.Sku;
        entity.Price = request.Price;
        entity.AllowBackorder = request.AllowBackorder;
        entity.EstimatedRestockDate = request.EstimatedRestockDate;
        entity.TaxCategoryId = request.TaxCategoryId;
        entity.ManufacturerId = request.ManufacturerId;
        entity.IsPublished = request.IsPublished;
        entity.ReviewsEnabled = request.ReviewsEnabled;
        entity.IsInquiryOnly = request.IsInquiryOnly;
        entity.InquiryButtonLabel = request.InquiryButtonLabel;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsFeatured = request.IsFeatured;
        entity.FeaturedDisplayOrder = request.FeaturedDisplayOrder;
        entity.DownloadExpiryDays = request.DownloadExpiryDays;
        entity.DownloadLimit = request.DownloadLimit;
        entity.GiftCardType = request.GiftCardType;
        entity.GiftCardAmount = request.GiftCardAmount;
        entity.H1 = request.H1;
        entity.MetaImageMediaId = request.MetaImageMediaId;
        entity.MetaOpenGraph = request.MetaOpenGraph;
        entity.MetaCustomTags = request.MetaCustomTags;
        entity.MetaFooterTags = request.MetaFooterTags;
        entity.Gtin = request.Gtin;
        entity.Cost = request.Cost;
        entity.Msrp = request.Msrp;
        entity.SalePrice = request.SalePrice;
        entity.SaleStartDateUtc = request.SaleStartDateUtc;
        entity.SaleEndDateUtc = request.SaleEndDateUtc;
        entity.TaxCode = request.TaxCode;
        entity.IsPurchasable = request.IsPurchasable;
        entity.IsFflRequired = request.IsFflRequired;
        entity.MinOrderQuantity = request.MinOrderQuantity;
        entity.MaxOrderQuantity = request.MaxOrderQuantity;
        entity.StockStatusOverride = request.StockStatusOverride;
        entity.BackorderMessage = request.BackorderMessage;
        entity.Weight = request.Weight;
        entity.Length = request.Length;
        entity.Width = request.Width;
        entity.Height = request.Height;
        entity.PackagePieces = request.PackagePieces;
        entity.PackageType = request.PackageType;
        entity.FreightClass = request.FreightClass;
        entity.IsHazardous = request.IsHazardous;

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(entity);
    }
}
