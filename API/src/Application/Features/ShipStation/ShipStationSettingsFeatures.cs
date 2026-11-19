using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ShipStation;

/// <summary>The ShipStation screen (§5.1) with the feed URL and the last export / shipnotify calls (SS-07).</summary>
public record GetShipStationSettingsQuery(string? FeedUrl = null) : IRequest<ShipStationSettingsDto>;

public class GetShipStationSettingsQueryHandler : IRequestHandler<GetShipStationSettingsQuery, ShipStationSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetShipStationSettingsQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ShipStationSettingsDto> Handle(GetShipStationSettingsQuery request, CancellationToken ct)
    {
        var dto = ShipStationSettingsDto.From(await ShipStationSupport.LoadAsync(_db, ct), _clock.UtcNow);
        dto.FeedUrl = request.FeedUrl;
        await AddLastCallsAsync(_db, dto, ct);
        return dto;
    }

    internal static async Task AddLastCallsAsync(IApplicationDbContext db, ShipStationSettingsDto dto, CancellationToken ct)
    {
        var lastExport = await db.ShipStationLogs.AsNoTracking()
            .Where(l => l.Action == ShipStationActions.Export)
            .OrderByDescending(l => l.CreatedOnUtc).FirstOrDefaultAsync(ct);
        var lastNotify = await db.ShipStationLogs.AsNoTracking()
            .Where(l => l.Action == ShipStationActions.ShipNotify)
            .OrderByDescending(l => l.CreatedOnUtc).FirstOrDefaultAsync(ct);
        dto.LastExport = lastExport is null ? null : ShipStationLogDto.From(lastExport);
        dto.LastShipNotify = lastNotify is null ? null : ShipStationLogDto.From(lastNotify);
    }
}

/// <summary>
/// Saves the ShipStation screen. A blank <see cref="Password"/> keeps the stored one (SS-50); username and a
/// password are required while enabled. Turning debug logging on keeps it on for 24 hours (SS-51).
/// The save is recorded by the admin audit trail, which names changed fields only (SS-56).
/// </summary>
public record UpdateShipStationSettingsCommand(
    bool Enabled,
    string? Username,
    string? Password,
    bool IncludeEbay,
    bool SplitFfl,
    bool NotifyCustomer,
    List<string>? ExportStatuses,
    string? TimeZone,
    bool AllowQueryCredentials,
    bool DebugLogging,
    string? FeedUrl = null) : IRequest<ShipStationSettingsDto>;

public class UpdateShipStationSettingsCommandValidator : AbstractValidator<UpdateShipStationSettingsCommand>
{
    public UpdateShipStationSettingsCommandValidator()
    {
        RuleFor(x => x.Username).MaximumLength(200);
        RuleFor(x => x.Username).NotEmpty().When(x => x.Enabled)
            .WithMessage("Enter the username ShipStation will use, or switch ShipStation off.");
        RuleFor(x => x.Password).MaximumLength(200);
        RuleFor(x => x.Password).MinimumLength(8).When(x => !string.IsNullOrEmpty(x.Password))
            .WithMessage("Use a password of at least 8 characters.");
        RuleForEach(x => x.ExportStatuses)
            .Must(s => ShipStationStatuses.All.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Unknown order status '{PropertyValue}'.");
        RuleFor(x => x.ExportStatuses).Must(s => s is { Count: > 0 }).When(x => x.Enabled)
            .WithMessage("Choose at least one order status to export.");
        RuleFor(x => x.TimeZone)
            .Must(tz => string.IsNullOrWhiteSpace(tz) || TimeZoneInfo.TryFindSystemTimeZoneById(tz.Trim(), out _))
            .WithMessage("Unknown time zone. Use an id such as America/New_York.");
    }
}

public class UpdateShipStationSettingsCommandHandler : IRequestHandler<UpdateShipStationSettingsCommand, ShipStationSettingsDto>
{
    private static readonly TimeSpan DebugWindow = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateShipStationSettingsCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ShipStationSettingsDto> Handle(UpdateShipStationSettingsCommand request, CancellationToken ct)
    {
        var settings = await _db.ShipStationSettings.OrderBy(s => s.CreatedOnUtc).FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new ShipStationSettings();
            _db.ShipStationSettings.Add(settings);
        }

        if (!string.IsNullOrEmpty(request.Password))
            settings.Password = request.Password;
        if (request.Enabled && string.IsNullOrEmpty(settings.Password))
            throw new Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.Password),
                    "Enter the password ShipStation will use, or switch ShipStation off."),
            });

        var now = _clock.UtcNow;
        settings.Enabled = request.Enabled;
        settings.Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        settings.IncludeEbay = request.IncludeEbay;
        settings.SplitFfl = request.SplitFfl;
        settings.NotifyCustomer = request.NotifyCustomer;
        settings.ExportStatuses = string.Join(",", ShipStationStatuses.All
            .Where(s => (request.ExportStatuses ?? new List<string>()).Contains(s, StringComparer.OrdinalIgnoreCase)));
        settings.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? ShipStationSettings.DefaultTimeZone : request.TimeZone.Trim();
        settings.AllowQueryCredentials = request.AllowQueryCredentials;
        if (!request.DebugLogging)
            settings.DebugUntilUtc = null;
        else if (settings.DebugUntilUtc is not DateTime until || until <= now)
            settings.DebugUntilUtc = now + DebugWindow;

        await _db.SaveChangesAsync(ct);

        var dto = ShipStationSettingsDto.From(settings, now);
        dto.FeedUrl = request.FeedUrl;
        await GetShipStationSettingsQueryHandler.AddLastCallsAsync(_db, dto, ct);
        return dto;
    }
}

/// <summary>The ShipStation call log for the admin (§6.1 <c>/activity</c>), newest first.</summary>
public record ListShipStationActivityQuery(
    DateTime? From = null, DateTime? To = null, string? Action = null, int Page = 1, int PageSize = 25)
    : IRequest<PaginatedList<ShipStationLogDto>>;

public class ListShipStationActivityQueryHandler : IRequestHandler<ListShipStationActivityQuery, PaginatedList<ShipStationLogDto>>
{
    private readonly IApplicationDbContext _db;

    public ListShipStationActivityQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ShipStationLogDto>> Handle(ListShipStationActivityQuery request, CancellationToken ct)
    {
        var query = _db.ShipStationLogs.AsNoTracking();
        if (request.From is DateTime from) query = query.Where(l => l.CreatedOnUtc >= from);
        if (request.To is DateTime to) query = query.Where(l => l.CreatedOnUtc <= to);
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(l => l.Action == request.Action);

        var page = await PaginatedList<ShipStationLog>.CreateAsync(
            query.OrderByDescending(l => l.CreatedOnUtc), Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 200), ct);
        return new PaginatedList<ShipStationLogDto>(
            page.Items.Select(ShipStationLogDto.From).ToList(), page.TotalCount, page.PageNumber, Math.Clamp(request.PageSize, 1, 200));
    }
}

public static class ShipStationActions
{
    public const string Export = "export";
    public const string ShipNotify = "shipnotify";
    public const string Auth = "auth";
}

public enum ShipStationAuthResult
{
    Ok,
    /// <summary>No credentials on the request (SS-05: 401).</summary>
    MissingCredentials,
    /// <summary>Wrong username or password (SS-02).</summary>
    BadCredentials,
    /// <summary>The integration is switched off (SS-03).</summary>
    Disabled,
}

/// <summary>What the endpoint needs to know about the settings for one call.</summary>
public record ShipStationAuthOutcome(ShipStationAuthResult Result, bool DebugLogging, bool AllowQueryCredentials);

/// <summary>
/// Checks the credentials of one ShipStation call with a constant-time comparison (SS-05). A switched-off
/// integration answers <see cref="ShipStationAuthResult.Disabled"/> whatever the credentials (SS-03).
/// </summary>
public record AuthenticateShipStationQuery(string? Username, string? Password, bool FromQueryString) : IRequest<ShipStationAuthOutcome>;

public class AuthenticateShipStationQueryHandler : IRequestHandler<AuthenticateShipStationQuery, ShipStationAuthOutcome>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AuthenticateShipStationQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ShipStationAuthOutcome> Handle(AuthenticateShipStationQuery request, CancellationToken ct)
    {
        var settings = await ShipStationSupport.LoadAsync(_db, ct);
        var debug = settings.DebugUntilUtc > _clock.UtcNow;

        ShipStationAuthResult result;
        if (!settings.Enabled)
            result = ShipStationAuthResult.Disabled;
        else if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password)
                 || (request.FromQueryString && !settings.AllowQueryCredentials))
            result = ShipStationAuthResult.MissingCredentials;
        else if (string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(settings.Password))
            result = ShipStationAuthResult.BadCredentials;
        else
        {
            // Evaluate both comparisons so the timing does not reveal which one failed.
            var userOk = FixedTimeEquals(request.Username, settings.Username);
            var passOk = FixedTimeEquals(request.Password, settings.Password);
            result = userOk & passOk ? ShipStationAuthResult.Ok : ShipStationAuthResult.BadCredentials;
        }

        return new ShipStationAuthOutcome(result, debug, settings.AllowQueryCredentials);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}

/// <summary>Writes one row of the ShipStation call log (SS-51).</summary>
public record LogShipStationCallCommand(
    string Action,
    int HttpStatus,
    long DurationMs,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int? Page = null,
    int? OrderCount = null,
    string? OrderNumber = null,
    string? Error = null,
    string? ClientIp = null) : IRequest;

public class LogShipStationCallCommandHandler : IRequestHandler<LogShipStationCallCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public LogShipStationCallCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Handle(LogShipStationCallCommand request, CancellationToken ct)
    {
        _db.ShipStationLogs.Add(new ShipStationLog
        {
            Action = request.Action,
            HttpStatus = request.HttpStatus,
            DurationMs = request.DurationMs,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Page = request.Page,
            OrderCount = request.OrderCount,
            OrderNumber = Truncate(request.OrderNumber, 64),
            Error = Truncate(request.Error, 1000),
            ClientIp = Truncate(request.ClientIp, 64),
            CreatedOnUtc = _clock.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
