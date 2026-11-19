using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>
/// The return policy (order-return-requirements section 15). Stored as one JSON platform setting
/// (<see cref="ReturnSettingsStore.Key"/>), so every save is cached, flushed and written to the settings history.
/// </summary>
public class ReturnSettingsDto
{
    /// <summary>Master switch for customer self-service. Staff can always create returns from the admin.</summary>
    public bool Enabled { get; set; }

    /// <summary>RT-10: days after the order shipped (falls back to the order date) that a line can be returned.</summary>
    public int WindowDays { get; set; } = 30;

    /// <summary>RT-11: order statuses that can be returned.</summary>
    public List<OrderStatus> EligibleStatuses { get; set; } = new() { OrderStatus.Shipped, OrderStatus.Delivered };

    /// <summary>RT-40: who supplies the return label.</summary>
    public ReturnLabelMode LabelMode { get; set; } = ReturnLabelMode.CustomerShips;
    /// <summary>Default carrier staff use for return labels.</summary>
    public string LabelCarrier { get; set; } = "USPS";

    /// <summary>RT-04: an approved return with no shipment after this many days expires.</summary>
    public int LabelExpiryDays { get; set; } = 30;

    /// <summary>Restocking fee as a % of the line refund, for reasons marked "fee applies".</summary>
    public decimal RestockFeePercent { get; set; }

    /// <summary>Photos are required for reasons marked "requires photo" (Damaged / Defective).</summary>
    public bool RequirePhotos { get; set; } = true;

    /// <summary>RT-25: auto-approve requests whose reasons allow it and whose estimate is at most <see cref="AutoApproveMax"/>.</summary>
    public bool AutoApprove { get; set; }
    public decimal AutoApproveMax { get; set; } = 100m;

    /// <summary>Resolutions the customer may pick.</summary>
    public bool AllowStoreCredit { get; set; } = true;
    public bool AllowExchange { get; set; } = true;
    public bool AllowDropOff { get; set; }

    /// <summary>RT-69: extra % added when the customer takes store credit instead of a refund.</summary>
    public decimal StoreCreditBonusPercent { get; set; }

    /// <summary>RT-81: staff address alerted on every new request and on a refund failure (comma separated).</summary>
    public string? NotifyEmail { get; set; }

    /// <summary>Return policy page linked from the request form.</summary>
    public string? PolicyUrl { get; set; }

    /// <summary>Where the customer ships returns to.</summary>
    public ReturnAddressDto ReturnAddress { get; set; } = new();

    /// <summary>Store whose stock is credited when the order has no assigned store.</summary>
    public Guid? DefaultRestockStoreId { get; set; }
}

public class ReturnAddressDto
{
    public string? Name { get; set; }
    public string? Company { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "US";
    public string? Phone { get; set; }

    /// <summary>The address as HTML lines for emails (values encoded).</summary>
    public string ToHtml()
    {
        var parts = new[] { Name, Company, Line1, Line2, string.Join(" ", new[] { City, State, PostalCode }.Where(s => !string.IsNullOrWhiteSpace(s))), Country, Phone }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => System.Net.WebUtility.HtmlEncode(s!));
        return string.Join("<br>", parts);
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Line1) && string.IsNullOrWhiteSpace(City);
}

/// <summary>What the storefront needs to render the request form (no staff-only values).</summary>
public class PublicReturnSettingsDto
{
    public bool Enabled { get; set; }
    public int WindowDays { get; set; }
    public ReturnLabelMode LabelMode { get; set; }
    public bool RequirePhotos { get; set; }
    public bool AllowStoreCredit { get; set; }
    public bool AllowExchange { get; set; }
    public bool AllowDropOff { get; set; }
    public decimal RestockFeePercent { get; set; }
    public decimal StoreCreditBonusPercent { get; set; }
    public string? PolicyUrl { get; set; }
}

public static class ReturnSettingsStore
{
    public const string Key = "Returns.Settings";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<ReturnSettingsDto> LoadAsync(ISettingsService settings, CancellationToken ct)
    {
        var raw = await settings.GetValueAsync(Key, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return new ReturnSettingsDto();
        try
        {
            return JsonSerializer.Deserialize<ReturnSettingsDto>(raw, Json) ?? new ReturnSettingsDto();
        }
        catch (JsonException)
        {
            return new ReturnSettingsDto();
        }
    }

    public static Task SaveAsync(ISettingsService settings, ReturnSettingsDto dto, CancellationToken ct)
        => settings.SetAsync(Key, JsonSerializer.Serialize(dto, Json), ct);
}

public record GetReturnSettingsQuery : IRequest<ReturnSettingsDto>;

public class GetReturnSettingsQueryHandler : IRequestHandler<GetReturnSettingsQuery, ReturnSettingsDto>
{
    private readonly ISettingsService _settings;
    public GetReturnSettingsQueryHandler(ISettingsService settings) => _settings = settings;

    public Task<ReturnSettingsDto> Handle(GetReturnSettingsQuery request, CancellationToken cancellationToken)
        => ReturnSettingsStore.LoadAsync(_settings, cancellationToken);
}

public record GetPublicReturnSettingsQuery : IRequest<PublicReturnSettingsDto>;

public class GetPublicReturnSettingsQueryHandler : IRequestHandler<GetPublicReturnSettingsQuery, PublicReturnSettingsDto>
{
    private readonly ISettingsService _settings;
    public GetPublicReturnSettingsQueryHandler(ISettingsService settings) => _settings = settings;

    public async Task<PublicReturnSettingsDto> Handle(GetPublicReturnSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = await ReturnSettingsStore.LoadAsync(_settings, cancellationToken);
        return new PublicReturnSettingsDto
        {
            Enabled = s.Enabled,
            WindowDays = s.WindowDays,
            LabelMode = s.LabelMode,
            RequirePhotos = s.RequirePhotos,
            AllowStoreCredit = s.AllowStoreCredit,
            AllowExchange = s.AllowExchange,
            AllowDropOff = s.AllowDropOff,
            RestockFeePercent = s.RestockFeePercent,
            StoreCreditBonusPercent = s.StoreCreditBonusPercent,
            PolicyUrl = s.PolicyUrl,
        };
    }
}

public record UpdateReturnSettingsCommand(ReturnSettingsDto Settings) : IRequest<ReturnSettingsDto>;

public class UpdateReturnSettingsCommandValidator : AbstractValidator<UpdateReturnSettingsCommand>
{
    public UpdateReturnSettingsCommandValidator()
    {
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.Settings.WindowDays).InclusiveBetween(1, 3650);
        RuleFor(x => x.Settings.LabelExpiryDays).InclusiveBetween(1, 365);
        RuleFor(x => x.Settings.RestockFeePercent).InclusiveBetween(0m, 100m);
        RuleFor(x => x.Settings.StoreCreditBonusPercent).InclusiveBetween(0m, 100m);
        RuleFor(x => x.Settings.AutoApproveMax).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Settings.EligibleStatuses).NotEmpty().WithMessage("Pick at least one order status that can be returned.");
        RuleFor(x => x.Settings.NotifyEmail).MaximumLength(500);
        RuleFor(x => x.Settings.PolicyUrl).MaximumLength(500);
    }
}

public class UpdateReturnSettingsCommandHandler : IRequestHandler<UpdateReturnSettingsCommand, ReturnSettingsDto>
{
    private readonly ISettingsService _settings;
    public UpdateReturnSettingsCommandHandler(ISettingsService settings) => _settings = settings;

    public async Task<ReturnSettingsDto> Handle(UpdateReturnSettingsCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Settings;
        dto.EligibleStatuses = dto.EligibleStatuses.Distinct().ToList();
        dto.LabelCarrier = string.IsNullOrWhiteSpace(dto.LabelCarrier) ? "USPS" : dto.LabelCarrier.Trim();
        dto.NotifyEmail = string.IsNullOrWhiteSpace(dto.NotifyEmail) ? null : dto.NotifyEmail.Trim();
        dto.PolicyUrl = string.IsNullOrWhiteSpace(dto.PolicyUrl) ? null : dto.PolicyUrl.Trim();
        await ReturnSettingsStore.SaveAsync(_settings, dto, cancellationToken);
        return await ReturnSettingsStore.LoadAsync(_settings, cancellationToken);
    }
}
