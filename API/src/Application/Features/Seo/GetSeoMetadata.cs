using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>
/// Admin read of one record's central SEO metadata. Returns an empty DTO (Exists = false) when the record
/// exists but has no SEO row yet; 404 when the owning record itself is missing.
/// </summary>
public record GetSeoMetadataQuery(SeoEntityType EntityType, Guid EntityId) : IRequest<SeoMetadataDto>;

public class GetSeoMetadataQueryHandler : IRequestHandler<GetSeoMetadataQuery, SeoMetadataDto>
{
    private readonly IApplicationDbContext _db;

    public GetSeoMetadataQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<SeoMetadataDto> Handle(GetSeoMetadataQuery request, CancellationToken cancellationToken)
    {
        var row = await _db.SeoMetadata
            .AsNoTracking()
            .Include(s => s.MetaImage)
            .FirstOrDefaultAsync(s => s.EntityType == request.EntityType && s.EntityId == request.EntityId, cancellationToken);
        if (row is not null)
            return SeoMetadataDto.From(row);

        if (!await SeoEntities.ExistsAsync(_db, request.EntityType, request.EntityId, cancellationToken))
            throw new NotFoundException(request.EntityType.ToString(), request.EntityId);

        return SeoMetadataDto.Empty(request.EntityType, request.EntityId);
    }
}
