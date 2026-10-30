using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Commerce;

/// <summary>
/// Updates the tenant's commerce mode and its inquiry options (REQ-INQ-001). Writes through
/// <see cref="ISettingsService"/> so each key is audited in the settings change history and the cache is
/// invalidated — the switch applies on the next request without a restart.
/// </summary>
public record UpdateCommerceModeCommand(
    string Mode,
    bool ShowPrices,
    bool CollectAddress,
    string? InquiryButtonLabel,
    Guid? DefaultStoreId,
    string? NotifyEmails,
    string? SubmitNote) : IRequest<CommerceModeDto>;

public class UpdateCommerceModeCommandValidator : AbstractValidator<UpdateCommerceModeCommand>
{
    public UpdateCommerceModeCommandValidator()
    {
        RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(m => Enum.TryParse<CommerceMode>(m, ignoreCase: true, out _))
            .WithMessage("Mode must be either 'Standard' or 'InquiryOnly'.");

        RuleFor(x => x.InquiryButtonLabel).MaximumLength(80);
        RuleFor(x => x.NotifyEmails).MaximumLength(1000);
        RuleFor(x => x.SubmitNote).MaximumLength(500);
    }
}

public class UpdateCommerceModeCommandHandler : IRequestHandler<UpdateCommerceModeCommand, CommerceModeDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly ICommerceModeService _commerce;

    public UpdateCommerceModeCommandHandler(
        IApplicationDbContext db, ISettingsService settings, ICommerceModeService commerce)
    {
        _db = db;
        _settings = settings;
        _commerce = commerce;
    }

    public async Task<CommerceModeDto> Handle(UpdateCommerceModeCommand request, CancellationToken cancellationToken)
    {
        var mode = Enum.Parse<CommerceMode>(request.Mode, ignoreCase: true);

        // The fallback store must exist and be usable, or inquiries submitted without an address would be
        // left unassigned and nobody would be notified.
        if (request.DefaultStoreId is Guid storeId)
        {
            var exists = await _db.Stores
                .AsNoTracking()
                .AnyAsync(s => s.Id == storeId && s.IsEnabled, cancellationToken);
            if (!exists)
                throw new NotFoundException("The selected default inquiry store does not exist or is disabled.");
        }

        await _settings.SetAsync(CommerceSettingKeys.Mode, mode.ToString(), cancellationToken);
        await _settings.SetAsync(CommerceSettingKeys.ShowPrices, request.ShowPrices.ToString(), cancellationToken);
        await _settings.SetAsync(CommerceSettingKeys.CollectAddress, request.CollectAddress.ToString(), cancellationToken);
        await _settings.SetAsync(
            CommerceSettingKeys.ButtonLabel,
            string.IsNullOrWhiteSpace(request.InquiryButtonLabel) ? "Request a Quote" : request.InquiryButtonLabel.Trim(),
            cancellationToken);
        await _settings.SetAsync(
            CommerceSettingKeys.DefaultStoreId,
            request.DefaultStoreId?.ToString() ?? string.Empty,
            cancellationToken);
        await _settings.SetAsync(CommerceSettingKeys.NotifyEmails, request.NotifyEmails?.Trim() ?? string.Empty, cancellationToken);
        await _settings.SetAsync(CommerceSettingKeys.SubmitNote, request.SubmitNote?.Trim() ?? string.Empty, cancellationToken);

        return CommerceModeDto.From(await _commerce.GetAsync(cancellationToken));
    }
}
