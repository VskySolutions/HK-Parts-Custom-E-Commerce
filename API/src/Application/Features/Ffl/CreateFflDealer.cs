using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Ffl;

/// <summary>
/// Adds one dealer to the directory by hand (<c>POST /api/admin/ffl/dealers</c>), for a dealer the buyer needs that
/// is not in the last ATF import yet. The licence number must be a full ATF number (R-DD-CCC-TT-XX-SSSSS, dashes
/// optional): it becomes the dealer's key, so a later import of the same licence turns the row into a normal ATF
/// record instead of duplicating it. The row is flagged <see cref="FflDealer.IsManual"/> so admins know to verify it
/// and "Deactivate dealers not in the file" leaves it alone. The premise ZIP is geocoded at once so the dealer is
/// searchable straight away.
/// </summary>
public record CreateFflDealerCommand(
    string LicenseNumber,
    string LicenseName,
    string? BusinessName,
    string Street,
    string City,
    string State,
    string Zip,
    string? Phone,
    bool AllowsTransfers = true,
    decimal? LastTransferFee = null) : IRequest<FflDealerDto>;

public static partial class FflLicenseNumber
{
    /// <summary>R-DD-CCC-TT-XX-SSSSS: region, district, county, type, expiry (year digit + month letter A–M, no I), sequence.</summary>
    [GeneratedRegex(@"^(\d)-?(\d{2})-?(\d{3})-?(\d{2})-?(\d[A-HJ-M])-?(\d{5})$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    public sealed record Parts(string Regn, string Dist, string Cnty, string Type, string Xprdte, string Seqn);

    public static Parts? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var m = Pattern().Match(value.Trim().Replace(" ", string.Empty));
        return m.Success
            ? new Parts(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value,
                m.Groups[5].Value.ToUpperInvariant(), m.Groups[6].Value)
            : null;
    }
}

public class CreateFflDealerCommandValidator : AbstractValidator<CreateFflDealerCommand>
{
    public CreateFflDealerCommandValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.LicenseNumber).NotEmpty().WithMessage("Dealer FFL Number required")
            .Must(v => FflLicenseNumber.Parse(v) is not null)
            .WithMessage("Enter the full FFL number as R-DD-CCC-TT-XX-SSSSS, e.g. 1-57-011-01-6K-12345.")
            .Must(v => FflLicenseNumber.Parse(v) is not { } p || FflDirectory.TransferLicenseTypes.Contains(p.Type))
            .WithMessage("This licence type can't receive transfers. Allowed types: " + string.Join(", ", FflDirectory.TransferLicenseTypes) + ".")
            .Must(v => FflLicenseNumber.Parse(v) is not { } p
                       || FflDirectory.LicenseExpiry(p.Xprdte, clock.UtcNow) is not { } e
                       || e >= DateOnly.FromDateTime(clock.UtcNow))
            .WithMessage("This licence has expired.");
        RuleFor(x => x.LicenseName).NotEmpty().WithMessage("Dealer Name required").MaximumLength(255);
        RuleFor(x => x.BusinessName).MaximumLength(255);
        RuleFor(x => x.Street).NotEmpty().WithMessage("Dealer Shipping Address required").MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().WithMessage("Dealer Shipping City required").MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().WithMessage("Dealer Shipping State required")
            .Matches("^[A-Za-z]{2}$").WithMessage("Use the two-letter state code.");
        RuleFor(x => x.Zip).NotEmpty().WithMessage("Dealer Shipping ZIP Code required")
            .Matches(@"^\d{5}(-?\d{4})?$").WithMessage(FflMessages.InvalidZip);
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Dealer Phone Number required")
            .Must(p => p is null || p.Count(char.IsDigit) is >= 10 and <= 15).WithMessage("Enter a valid phone number.");
        RuleFor(x => x.LastTransferFee).GreaterThanOrEqualTo(0).LessThan(100000).When(x => x.LastTransferFee.HasValue)
            .WithMessage("Transfer fee must be a positive amount.");
    }
}

public class CreateFflDealerCommandHandler : IRequestHandler<CreateFflDealerCommand, FflDealerDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IZipGeocoder _geocoder;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CreateFflDealerCommandHandler> _logger;

    public CreateFflDealerCommandHandler(
        IApplicationDbContext db, IZipGeocoder geocoder, IDateTimeProvider clock, ILogger<CreateFflDealerCommandHandler> logger)
    {
        _db = db;
        _geocoder = geocoder;
        _clock = clock;
        _logger = logger;
    }

    public async Task<FflDealerDto> Handle(CreateFflDealerCommand request, CancellationToken ct)
    {
        var lic = FflLicenseNumber.Parse(request.LicenseNumber)!;
        var key = FflDirectory.DealerKey(lic.Regn, lic.Dist, lic.Seqn);

        if (await FflDirectory.FindAsync(_db, key, ct) is { } existing)
            throw new ConflictException(
                $"{FflDirectory.DisplayName(existing)} (FFL {FflDirectory.BuildFflNumber(existing)}) is already in the directory" +
                (existing.IsActive ? "." : " but inactive; a new ATF import that includes it will reactivate it."));

        var now = _clock.UtcNow;
        var zip = request.Zip.Trim();
        var dealer = new FflDealer
        {
            Key = key,
            LicRegn = lic.Regn,
            LicDist = lic.Dist,
            LicCnty = lic.Cnty,
            LicType = lic.Type,
            LicXprdte = lic.Xprdte,
            LicSeqn = lic.Seqn,
            LicenseName = request.LicenseName.Trim(),
            BusinessName = string.IsNullOrWhiteSpace(request.BusinessName) ? null : request.BusinessName.Trim(),
            PremiseStreet = request.Street.Trim(),
            PremiseCity = request.City.Trim(),
            PremiseState = request.State.Trim().ToUpperInvariant(),
            PremiseZipCode = zip,
            VoicePhone = new string(request.Phone!.Where(char.IsDigit).ToArray()),
            IsActive = true,
            IsManual = true,
            AllowsTransfers = request.AllowsTransfers,
            LastTransferFee = request.LastTransferFee is { } fee ? decimal.Round(fee, 2) : null,
            DealerDataUpdatedOnUtc = now,
        };

        // Searchable straight away: the ZIP centre from the cache, the geocoder, or the directory's own dealers.
        if (FflDirectory.NormalizeZip(zip) is { } zip5
            && (await FflDirectory.ResolveZipAsync(_db, _geocoder, zip5, _logger, ct)).Point is { } at)
        {
            dealer.Latitude = at.Latitude;
            dealer.Longitude = at.Longitude;
        }

        _db.FflDealers.Add(dealer);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FFL dealer {Key} added by hand (geocoded: {Geocoded}).", key, dealer.Latitude.HasValue);
        return FflDirectory.ToDto(dealer, now);
    }
}
