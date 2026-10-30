using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Settings;

public record GetSettingQuery(string Key) : IRequest<SettingDto>;

public class GetSettingQueryHandler : IRequestHandler<GetSettingQuery, SettingDto>
{
    private readonly IApplicationDbContext _db;

    public GetSettingQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<SettingDto> Handle(GetSettingQuery request, CancellationToken cancellationToken)
    {
        var setting = await _db.PlatformSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == request.Key, cancellationToken)
            ?? throw new NotFoundException("Setting", request.Key);

        return SettingDto.From(setting);
    }
}
