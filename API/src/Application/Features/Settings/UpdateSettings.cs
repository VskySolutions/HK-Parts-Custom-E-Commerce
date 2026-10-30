using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Settings;

/// <summary>Bulk-updates multiple settings by key (each audited and cache-invalidated).</summary>
public record UpdateSettingsCommand(IReadOnlyDictionary<string, string?> Settings)
    : IRequest<IReadOnlyList<SettingDto>>;

public class UpdateSettingsCommandHandler : IRequestHandler<UpdateSettingsCommand, IReadOnlyList<SettingDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;

    public UpdateSettingsCommandHandler(IApplicationDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<IReadOnlyList<SettingDto>> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        var keys = request.Settings.Keys.ToList();
        var known = await _db.PlatformSettings
            .Where(s => keys.Contains(s.Key))
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);

        var unknown = keys.Except(known).ToList();
        if (unknown.Count > 0)
            throw new NotFoundException($"Unknown setting keys: {string.Join(", ", unknown)}.");

        foreach (var (key, value) in request.Settings)
            await _settings.SetAsync(key, value, cancellationToken);

        var updated = await _db.PlatformSettings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return updated.Select(SettingDto.From).ToList();
    }
}
