using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Media;

/// <summary>Returns a single media row (with its resolved public URL) — powers the SEO/metadata editor.</summary>
public record GetMediaQuery(Guid Id) : IRequest<MediaDto>;

public class GetMediaQueryHandler : IRequestHandler<GetMediaQuery, MediaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public GetMediaQueryHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<MediaDto> Handle(GetMediaQuery request, CancellationToken cancellationToken)
    {
        var media = await _db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Media), request.Id);

        var url = await _storage.GetFileUrlAsync(media.AssetKey, cancellationToken);
        return MediaDto.From(media, url);
    }
}
