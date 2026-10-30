using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.FeaturedContent;

/// <summary>Marks (or unmarks) a category for the storefront "Featured Categories" showcase (WO-98).</summary>
public record SetCategoryFeaturedCommand(Guid CategoryId, bool IsFeatured) : IRequest<FeaturedCategoryDto>;

public class SetCategoryFeaturedCommandValidator : AbstractValidator<SetCategoryFeaturedCommand>
{
    public SetCategoryFeaturedCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

public class SetCategoryFeaturedCommandHandler : IRequestHandler<SetCategoryFeaturedCommand, FeaturedCategoryDto>
{
    private readonly IApplicationDbContext _db;

    public SetCategoryFeaturedCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<FeaturedCategoryDto> Handle(SetCategoryFeaturedCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.CategoryId);

        entity.IsFeatured = request.IsFeatured;
        await _db.SaveChangesAsync(cancellationToken);

        // Re-project so the response carries the resolved image, consistent with the featured-categories list.
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == entity.Id)
            .Select(FeaturedCategoryDto.Projection(_db))
            .FirstAsync(cancellationToken);
    }
}
