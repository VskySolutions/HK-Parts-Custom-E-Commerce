using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ProductReviews;

/// <summary>Admin toggle for whether a product accepts and displays reviews (WO-14): flips
/// <see cref="Product.ReviewsEnabled"/>. When turned off the storefront hides the reviews section and
/// new submissions are refused.</summary>
public record SetProductReviewsEnabledCommand(Guid ProductId, bool Enabled) : IRequest;

public class SetProductReviewsEnabledCommandValidator : AbstractValidator<SetProductReviewsEnabledCommand>
{
    public SetProductReviewsEnabledCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class SetProductReviewsEnabledCommandHandler : IRequestHandler<SetProductReviewsEnabledCommand>
{
    private readonly IApplicationDbContext _db;

    public SetProductReviewsEnabledCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SetProductReviewsEnabledCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        product.ReviewsEnabled = request.Enabled;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
