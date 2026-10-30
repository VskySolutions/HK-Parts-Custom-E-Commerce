using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.FeaturedContent;

/// <summary>
/// Sets (or clears) a product's storefront "featured" designation and, optionally, its position among
/// featured products (WO-98, REQ-CNT-011). When no explicit order is supplied the existing
/// <see cref="Product.FeaturedDisplayOrder"/> is left untouched.
/// </summary>
public record SetProductFeaturedCommand(Guid ProductId, bool IsFeatured, int? FeaturedDisplayOrder = null)
    : IRequest<FeaturedProductDto>;

public class SetProductFeaturedCommandValidator : AbstractValidator<SetProductFeaturedCommand>
{
    public SetProductFeaturedCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.FeaturedDisplayOrder)
            .GreaterThanOrEqualTo(0)
            .When(x => x.FeaturedDisplayOrder.HasValue);
    }
}

public class SetProductFeaturedCommandHandler : IRequestHandler<SetProductFeaturedCommand, FeaturedProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductFeaturedCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<FeaturedProductDto> Handle(SetProductFeaturedCommand request, CancellationToken cancellationToken)
    {
        // Tracked load (with the summary media) so the mutation persists and the response resolves an image.
        var entity = await _db.Products
            .Include(p => p.Pictures.Where(i => i.ProductVariantId == null))
                .ThenInclude(pic => pic.Media)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        entity.IsFeatured = request.IsFeatured;
        if (request.FeaturedDisplayOrder.HasValue)
            entity.FeaturedDisplayOrder = request.FeaturedDisplayOrder.Value;

        await _db.SaveChangesAsync(cancellationToken);
        return FeaturedProductDto.From(entity);
    }
}
