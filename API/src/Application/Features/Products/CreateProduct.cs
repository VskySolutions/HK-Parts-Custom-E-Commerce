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
    decimal? GiftCardAmount = null) : IRequest<ProductDto>;

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
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(entity);
    }
}
