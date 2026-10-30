using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsHomeSections;

/// <summary>Fetches a single home page section by id (with its config deserialized).</summary>
public record GetHomeSectionQuery(Guid Id) : IRequest<CmsHomeSectionDto>;

public class GetHomeSectionQueryHandler : IRequestHandler<GetHomeSectionQuery, CmsHomeSectionDto>
{
    private readonly IApplicationDbContext _db;

    public GetHomeSectionQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsHomeSectionDto> Handle(GetHomeSectionQuery request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSHomePageSections
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSHomePageSection), request.Id);

        return CmsHomeSectionDto.From(entity);
    }
}
