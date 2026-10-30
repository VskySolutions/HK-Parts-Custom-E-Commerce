using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductReviews;

/// <summary>
/// Can the current signed-in customer write a review for this product (WO-14)? Returns a structured
/// eligibility result so the storefront can disable the "Write a Review" button up-front with a reason,
/// instead of the shopper only discovering they can't when the submit is rejected. The rules mirror
/// <see cref="SubmitProductReviewCommand"/> exactly — keep the two in sync.
/// </summary>
public record GetProductReviewEligibilityQuery(Guid ProductId) : IRequest<ProductReviewEligibilityDto>;

public class GetProductReviewEligibilityQueryHandler : IRequestHandler<GetProductReviewEligibilityQuery, ProductReviewEligibilityDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetProductReviewEligibilityQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<ProductReviewEligibilityDto> Handle(GetProductReviewEligibilityQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            return ProductReviewEligibilityDto.Denied("NotSignedIn", "Sign in to write a review.");

        var customer = await _db.Customers.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
            return ProductReviewEligibilityDto.Denied("NoProfile", "Your account can't post reviews.");

        var product = await _db.Products.AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Id, p.ReviewsEnabled })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.ReviewsEnabled)
            return ProductReviewEligibilityDto.Denied("ReviewsDisabled", "Reviews are turned off for this product.");

        // A genuine purchase = a (non-deleted) order for this customer that contains the product and whose
        // payment was collected (Captured or PartiallyRefunded) — same "money in" rule as the submit handler.
        var hasPurchased = await _db.Orders.AsNoTracking()
            .AnyAsync(o => o.CustomerId == customer.Id
                && (o.PaymentStatus == PaymentStatus.Captured || o.PaymentStatus == PaymentStatus.PartiallyRefunded)
                && o.Lines.Any(l => l.ProductId == request.ProductId), cancellationToken);
        if (!hasPurchased)
            return ProductReviewEligibilityDto.Denied("NotPurchased", "You can only review products you have purchased.");

        var alreadyReviewed = await _db.ProductReviews.AsNoTracking()
            .AnyAsync(r => r.ProductId == request.ProductId
                && r.CustomerId == customer.Id
                && (r.Status == ReviewStatus.Pending || r.Status == ReviewStatus.Approved), cancellationToken);
        if (alreadyReviewed)
            return ProductReviewEligibilityDto.Denied("AlreadyReviewed", "You have already reviewed this product.");

        return ProductReviewEligibilityDto.Ok();
    }
}
