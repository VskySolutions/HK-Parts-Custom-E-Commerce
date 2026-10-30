using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>Rewrites the collection's item ordering: each product's DisplayOrder becomes its index in the
/// supplied list. Ids not currently in the collection are ignored; items omitted from the list keep their
/// previous order.</summary>
public record ReorderCollectionItemsCommand(Guid CollectionId, List<Guid> OrderedProductIds) : IRequest<CmsProductCollectionDto>;

public class ReorderCollectionItemsCommandValidator : AbstractValidator<ReorderCollectionItemsCommand>
{
    public ReorderCollectionItemsCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty();
        RuleFor(x => x.OrderedProductIds).NotNull();
    }
}

public class ReorderCollectionItemsCommandHandler : IRequestHandler<ReorderCollectionItemsCommand, CmsProductCollectionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ReorderCollectionItemsCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CmsProductCollectionDto> Handle(ReorderCollectionItemsCommand request, CancellationToken cancellationToken)
    {
        var collection = await _db.CMSProductCollections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSProductCollection), request.CollectionId);

        var orderedIds = request.OrderedProductIds ?? new List<Guid>();
        for (var index = 0; index < orderedIds.Count; index++)
        {
            var item = collection.Items.FirstOrDefault(i => i.ProductId == orderedIds[index]);
            if (item is not null)
                item.DisplayOrder = index;
        }

        collection.UpdatedOnUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var detail = await _db.CMSProductCollections
            .AsNoTracking()
            .WithItemsAndMedia()
            .FirstAsync(c => c.Id == request.CollectionId, cancellationToken);
        return CmsProductCollectionDto.From(detail);
    }
}
