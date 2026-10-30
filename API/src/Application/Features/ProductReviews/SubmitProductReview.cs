using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductReviews;

/// <summary>
/// Submits a review for a product on behalf of the signed-in customer (WO-14). Only a verified purchaser
/// of the product may review it, reviews are only accepted while the product has reviews enabled, and a
/// customer may hold at most one active (pending or approved) review per product. New reviews start
/// <see cref="ReviewStatus.Pending"/> and are invisible on the storefront until an admin approves them.
/// </summary>
public record SubmitProductReviewCommand(
    Guid ProductId,
    int Rating,
    string? Title,
    string Body) : IRequest<ProductReviewDto>;

public class SubmitProductReviewCommandValidator : AbstractValidator<SubmitProductReviewCommand>
{
    public SubmitProductReviewCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Title).MaximumLength(200);
    }
}

public class SubmitProductReviewCommandHandler : IRequestHandler<SubmitProductReviewCommand, ProductReviewDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public SubmitProductReviewCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<ProductReviewDto> Handle(SubmitProductReviewCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new ForbiddenAccessException("You must be signed in to submit a review.");

        var customer = await _db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new { c.Id, c.FirstName, c.LastName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Id, p.ReviewsEnabled })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.ReviewsEnabled)
            throw new ConflictException("Reviews are disabled for this product.");

        // A genuine purchase = a (non-deleted) order for this customer that contains the product and whose
        // payment was actually collected. Mirrors the "money in" semantics used by the customer total-spent
        // rollups (GetCustomerDetail): Captured or PartiallyRefunded.
        var hasPurchased = await _db.Orders
            .AsNoTracking()
            .AnyAsync(o => o.CustomerId == customer.Id
                && (o.PaymentStatus == PaymentStatus.Captured || o.PaymentStatus == PaymentStatus.PartiallyRefunded)
                && o.Lines.Any(l => l.ProductId == request.ProductId), cancellationToken);

        if (!hasPurchased)
            throw new ForbiddenAccessException("You can only review products you have purchased.");

        var alreadyReviewed = await _db.ProductReviews
            .AsNoTracking()
            .AnyAsync(r => r.ProductId == request.ProductId
                && r.CustomerId == customer.Id
                && (r.Status == ReviewStatus.Pending || r.Status == ReviewStatus.Approved), cancellationToken);

        if (alreadyReviewed)
            throw new ConflictException("You have already submitted a review for this product.");

        var entity = new ProductReview
        {
            ProductId = request.ProductId,
            CustomerId = customer.Id,
            Rating = request.Rating,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Body = request.Body.Trim(),
            Status = ReviewStatus.Pending,
            ReviewerName = $"{customer.FirstName} {customer.LastName}".Trim(),
        };

        _db.ProductReviews.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ProductReviewDto.From(entity);
    }
}
