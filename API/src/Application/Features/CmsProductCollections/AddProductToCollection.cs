using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>Appends a product to a collection at the end of the current order (max DisplayOrder + 1).
/// Rejected with a conflict when the product is already in the collection.</summary>
public record AddProductToCollectionCommand(Guid CollectionId, Guid ProductId) : IRequest<CmsProductCollectionDto>;

public class AddProductToCollectionCommandValidator : AbstractValidator<AddProductToCollectionCommand>
{
    public AddProductToCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class AddProductToCollectionCommandHandler : IRequestHandler<AddProductToCollectionCommand, CmsProductCollectionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AddProductToCollectionCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CmsProductCollectionDto> Handle(AddProductToCollectionCommand request, CancellationToken cancellationToken)
    {
        // Load aggregate, mutate tracked children, save (safe under the AppDbContext Guid-key fix).
        var collection = await _db.CMSProductCollections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSProductCollection), request.CollectionId);

        // The product must exist (the global query filter already excludes soft-deleted products).
        if (!await _db.Products.AsNoTracking().AnyAsync(p => p.Id == request.ProductId, cancellationToken))
            throw new NotFoundException(nameof(Product), request.ProductId);

        if (collection.Items.Any(i => i.ProductId == request.ProductId))
            throw new ConflictException($"Product {request.ProductId} is already in collection {request.CollectionId}.");

        var nextOrder = collection.Items.Count == 0 ? 0 : collection.Items.Max(i => i.DisplayOrder) + 1;
        collection.Items.Add(new CMSProductCollectionItem
        {
            CollectionId = collection.Id,
            ProductId = request.ProductId,
            DisplayOrder = nextOrder,
        });

        // Bump the parent so the admin list's UpdatedOnUtc reflects the item change (items alone are not audited).
        collection.UpdatedOnUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(request.CollectionId, cancellationToken);
    }

    private async Task<CmsProductCollectionDto> ReloadAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        var detail = await _db.CMSProductCollections
            .AsNoTracking()
            .WithItemsAndMedia()
            .FirstAsync(c => c.Id == collectionId, cancellationToken);
        return CmsProductCollectionDto.From(detail);
    }
}
