using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductReviews;

/// <summary>
/// Public storefront reviews for a product (WO-14): its approved reviews (newest first) plus an aggregate
/// rating summary. When the product has reviews disabled the result carries <c>Summary.Enabled == false</c>
/// and no reviews, so the storefront can hide the whole section.
/// </summary>
public record GetProductReviewsQuery(Guid ProductId) : IRequest<ProductReviewListResultDto>;

public class GetProductReviewsQueryHandler : IRequestHandler<GetProductReviewsQuery, ProductReviewListResultDto>
{
    private readonly IApplicationDbContext _db;

    public GetProductReviewsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductReviewListResultDto> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Id, p.ReviewsEnabled })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.ReviewsEnabled)
            return new ProductReviewListResultDto { Summary = ProductReviewSummaryDto.Disabled() };

        var approved = await _db.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == request.ProductId && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return new ProductReviewListResultDto
        {
            Summary = ProductReviewSummaryDto.FromApproved(approved),
            Reviews = approved.Select(ProductReviewPublicDto.From).ToList(),
        };
    }
}
