using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.FeaturedContent;

/// <summary>
/// Rewrites the featured-product ordering: each id's position in <see cref="OrderedProductIds"/> becomes
/// its new <c>FeaturedDisplayOrder</c> (WO-98, AC-CNT-011.2). Only currently-featured products are
/// affected; ids that are not featured (or not found) are ignored, and featured products omitted from the
/// list keep their existing order.
/// </summary>
public record ReorderFeaturedProductsCommand(IReadOnlyList<Guid> OrderedProductIds) : IRequest;

public class ReorderFeaturedProductsCommandValidator : AbstractValidator<ReorderFeaturedProductsCommand>
{
    public ReorderFeaturedProductsCommandValidator()
    {
        RuleFor(x => x.OrderedProductIds).NotNull();
    }
}

public class ReorderFeaturedProductsCommandHandler : IRequestHandler<ReorderFeaturedProductsCommand>
{
    private readonly IApplicationDbContext _db;

    public ReorderFeaturedProductsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(ReorderFeaturedProductsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.OrderedProductIds?.ToList() ?? new List<Guid>();
        if (ids.Count == 0)
            return;

        // Position of each id in the requested order (last wins on any duplicate).
        var positionById = new Dictionary<Guid, int>();
        for (var i = 0; i < ids.Count; i++)
            positionById[ids[i]] = i;

        var products = await _db.Products
            .Where(p => p.IsFeatured && ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        foreach (var product in products)
            product.FeaturedDisplayOrder = positionById[product.Id];

        await _db.SaveChangesAsync(cancellationToken);
    }
}
