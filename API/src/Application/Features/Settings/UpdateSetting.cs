using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Settings;

/// <summary>Updates a single platform setting (audited, cache-invalidating).</summary>
public record UpdateSettingCommand(string Key, string? Value) : IRequest<SettingDto>;

public class UpdateSettingCommandValidator : AbstractValidator<UpdateSettingCommand>
{
    public UpdateSettingCommandValidator() => RuleFor(x => x.Key).NotEmpty().MaximumLength(200);
}

public class UpdateSettingCommandHandler : IRequestHandler<UpdateSettingCommand, SettingDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;

    public UpdateSettingCommandHandler(IApplicationDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<SettingDto> Handle(UpdateSettingCommand request, CancellationToken cancellationToken)
    {
        var exists = await _db.PlatformSettings.AnyAsync(s => s.Key == request.Key, cancellationToken);
        if (!exists)
            throw new NotFoundException("Setting", request.Key);

        await _settings.SetAsync(request.Key, request.Value, cancellationToken);

        var updated = await _db.PlatformSettings
            .AsNoTracking()
            .FirstAsync(s => s.Key == request.Key, cancellationToken);

        return SettingDto.From(updated);
    }
}
