using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Settings;

public record GetSettingsQuery(string? Category = null) : IRequest<IReadOnlyList<SettingDto>>;

public class GetSettingsQueryHandler : IRequestHandler<GetSettingsQuery, IReadOnlyList<SettingDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSettingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SettingDto>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.PlatformSettings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(s => s.Category == request.Category);

        var settings = await query
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return settings.Select(SettingDto.From).ToList();
    }
}
