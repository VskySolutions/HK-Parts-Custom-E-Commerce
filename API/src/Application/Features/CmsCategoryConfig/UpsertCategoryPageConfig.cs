using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsCategoryConfig;

/// <summary>
/// Creates or updates the dynamic page configuration for a category (WO-99) — one row per category. The
/// pinned-products child set is rewritten to match <see cref="PinnedProductIds"/> exactly, with each
/// product's DisplayOrder set to its index in the list.
/// </summary>
public record UpsertCategoryPageConfigCommand(
    Guid CategoryId,
    Guid? BannerMediaId,
    string? PromotionalDescription,
    Guid? YmalCollectionId,
    IReadOnlyList<Guid> PinnedProductIds) : IRequest<CmsCategoryPageConfigDto>;

public class UpsertCategoryPageConfigCommandValidator : AbstractValidator<UpsertCategoryPageConfigCommand>
{
    public UpsertCategoryPageConfigCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.PinnedProductIds).NotNull();
    }
}

public class UpsertCategoryPageConfigCommandHandler : IRequestHandler<UpsertCategoryPageConfigCommand, CmsCategoryPageConfigDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpsertCategoryPageConfigCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CmsCategoryPageConfigDto> Handle(UpsertCategoryPageConfigCommand request, CancellationToken cancellationToken)
    {
        // The config is anchored to a category; the banner asset and YMAL collection, when chosen, must exist.
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
            throw new NotFoundException(nameof(Category), request.CategoryId);

        if (request.YmalCollectionId is Guid ymalId
            && !await _db.CMSProductCollections.AnyAsync(c => c.Id == ymalId, cancellationToken))
            throw new NotFoundException(nameof(CMSProductCollection), ymalId);

        if (request.BannerMediaId is Guid bannerId
            && !await _db.Media.AnyAsync(m => m.Id == bannerId, cancellationToken))
            throw new NotFoundException(nameof(Media), bannerId);

        // Load the aggregate (with its pinned children) to create-or-update, then rewrite the child set in place.
        var config = await _db.CMSCategoryPageConfigs
            .Include(c => c.PinnedProducts)
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId, cancellationToken);

        if (config is null)
        {
            config = new CMSCategoryPageConfig { CategoryId = request.CategoryId };
            _db.CMSCategoryPageConfigs.Add(config);
        }

        config.BannerMediaId = request.BannerMediaId;
        config.PromotionalDescription = request.PromotionalDescription;
        config.YmalCollectionId = request.YmalCollectionId;

        // Rewrite the pinned set to match the supplied order: DisplayOrder = index. De-duplicated (first
        // occurrence wins) so a repeated id can't violate the (config, product) unique index.
        var desiredIds = (request.PinnedProductIds ?? Array.Empty<Guid>()).Distinct().ToList();

        var toRemove = config.PinnedProducts.Where(p => !desiredIds.Contains(p.ProductId)).ToList();
        foreach (var row in toRemove)
            config.PinnedProducts.Remove(row);

        for (var index = 0; index < desiredIds.Count; index++)
        {
            var productId = desiredIds[index];
            var existing = config.PinnedProducts.FirstOrDefault(p => p.ProductId == productId);
            if (existing is null)
                config.PinnedProducts.Add(new CMSCategoryPinnedProduct { ProductId = productId, DisplayOrder = index });
            else
                existing.DisplayOrder = index;
        }

        // Force the parent's audit timestamp to move even when only the pinned children changed (the config's
        // own scalars may be unchanged, leaving it Unchanged and un-audited otherwise).
        config.UpdatedOnUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Re-read with the full detail graph so the returned DTO carries resolved pinned rows, banner URL and
        // YMAL collection name.
        var detail = await _db.CMSCategoryPageConfigs
            .AsNoTracking()
            .WithDetails()
            .FirstAsync(c => c.CategoryId == request.CategoryId, cancellationToken);
        return CmsCategoryPageConfigDto.From(detail);
    }
}
