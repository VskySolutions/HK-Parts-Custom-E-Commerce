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
    decimal? GiftCardAmount = null) : IRequest<ProductDto>;

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

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(entity);
    }
}
