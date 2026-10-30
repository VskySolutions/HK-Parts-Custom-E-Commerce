using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>Removes a product from a collection (idempotent — a no-op when the product is not in it). The
/// remaining items keep their existing order.</summary>
public record RemoveProductFromCollectionCommand(Guid CollectionId, Guid ProductId) : IRequest<CmsProductCollectionDto>;

public class RemoveProductFromCollectionCommandValidator : AbstractValidator<RemoveProductFromCollectionCommand>
{
    public RemoveProductFromCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class RemoveProductFromCollectionCommandHandler : IRequestHandler<RemoveProductFromCollectionCommand, CmsProductCollectionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public RemoveProductFromCollectionCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CmsProductCollectionDto> Handle(RemoveProductFromCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await _db.CMSProductCollections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSProductCollection), request.CollectionId);

        var item = collection.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (item is not null)
        {
            // Removing from the tracked parent's collection cascades a delete of the (non-soft-delete) item row.
            collection.Items.Remove(item);
            collection.UpdatedOnUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var detail = await _db.CMSProductCollections
            .AsNoTracking()
            .WithItemsAndMedia()
            .FirstAsync(c => c.Id == request.CollectionId, cancellationToken);
        return CmsProductCollectionDto.From(detail);
    }
}
