using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsPages;

public record GetCmsPageQuery(Guid Id) : IRequest<CmsPageDto>;

public class GetCmsPageQueryHandler : IRequestHandler<GetCmsPageQuery, CmsPageDto>
{
    private readonly IApplicationDbContext _db;

    public GetCmsPageQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsPageDto> Handle(GetCmsPageQuery request, CancellationToken cancellationToken)
    {
        var page = await _db.CMSPages
            .AsNoTracking()
            .Include(p => p.PageGroup)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSPage), request.Id);

        return CmsPageDto.From(page);
    }
}
