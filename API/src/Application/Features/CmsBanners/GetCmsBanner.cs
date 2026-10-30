using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsBanners;

public record GetCmsBannerQuery(Guid Id) : IRequest<CmsBannerDto>;

public class GetCmsBannerQueryHandler : IRequestHandler<GetCmsBannerQuery, CmsBannerDto>
{
    private readonly IApplicationDbContext _db;

    public GetCmsBannerQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsBannerDto> Handle(GetCmsBannerQuery request, CancellationToken cancellationToken)
    {
        var banner = await _db.CMSBanners
            .AsNoTracking()
            .Include(b => b.ImageMedia)
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSBanner), request.Id);

        return CmsBannerDto.From(banner);
    }
}
