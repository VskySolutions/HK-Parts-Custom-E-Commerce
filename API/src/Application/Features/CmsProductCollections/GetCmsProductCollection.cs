using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>Returns a single product collection with its ordered items (each item's product name, SKU and
/// primary image) so the admin editor can render the ordered list.</summary>
public record GetCmsProductCollectionQuery(Guid Id) : IRequest<CmsProductCollectionDto>;

public class GetCmsProductCollectionQueryHandler : IRequestHandler<GetCmsProductCollectionQuery, CmsProductCollectionDto>
{
    private readonly IApplicationDbContext _db;

    public GetCmsProductCollectionQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsProductCollectionDto> Handle(GetCmsProductCollectionQuery request, CancellationToken cancellationToken)
    {
        var collection = await _db.CMSProductCollections
            .AsNoTracking()
            .WithItemsAndMedia()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSProductCollection), request.Id);

        return CmsProductCollectionDto.From(collection);
    }
}
