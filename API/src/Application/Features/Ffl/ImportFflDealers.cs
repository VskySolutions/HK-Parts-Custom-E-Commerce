using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Ffl;

/// <summary>Outcome of a directory import.</summary>
public class ImportFflDealersResult
{
    public int RowsRead { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Deactivated { get; set; }
    public int WithoutGeocode { get; set; }
    /// <summary>The first few row problems, for the admin to fix the file.</summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Loads the ATF licensee list into the dealer directory (FFL-113). Accepts the ATF monthly file (tab
/// delimited, LIC_REGN … VOICE_PHONE) or a CSV / pipe export of the legacy <c>ffl_database</c> table; columns are
/// matched by header name in any order, and optional <c>latitude</c> / <c>longitude</c> / dealer-data columns are
/// read when present. Rows are upserted by key (region + district + sequence). The store's own dealer data is
/// only written from columns the file actually carries, so a plain ATF refresh never resets it. With
/// <see cref="DeactivateMissing"/>, dealers absent from the file are marked inactive (never deleted).
/// </summary>
public record ImportFflDealersCommand(Stream Content, bool DeactivateMissing) : IRequest<ImportFflDealersResult>;

public class ImportFflDealersCommandHandler : IRequestHandler<ImportFflDealersCommand, ImportFflDealersResult>
{
    private const int MaxErrors = 20;

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ImportFflDealersCommandHandler> _logger;

    public ImportFflDealersCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ILogger<ImportFflDealersCommandHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ImportFflDealersResult> Handle(ImportFflDealersCommand request, CancellationToken ct)
    {
        var result = new ImportFflDealersResult();
        using var reader = new StreamReader(request.Content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            result.Errors.Add("The file is empty.");
            return result;
        }

        var delimiter = DetectDelimiter(headerLine);
        var headers = Split(headerLine, delimiter).Select(Normalize).ToList();
        int Col(params string[] names) => names.Select(n => headers.IndexOf(n)).FirstOrDefault(i => i >= 0, -1);

        var c = new Columns(
            Col("licregn"), Col("licdist"), Col("liccnty"), Col("lictype"), Col("licxprdte"), Col("licseqn"),
            Col("licensename"), Col("businessname"), Col("premisestreet"), Col("premisecity"), Col("premisestate"),
            Col("premisezipcode", "premisezip"), Col("mailstreet"), Col("mailcity"), Col("mailstate"), Col("mailzipcode", "mailzip"),
            Col("voicephone", "phone"), Col("latitude", "lat"), Col("longitude", "lng", "lon"),
            Col("allowstransfers"), Col("lasttransferfee"), Col("averagereviewrating"));

        if (c.Regn < 0 || c.Dist < 0 || c.Seqn < 0 || c.Type < 0)
        {
            result.Errors.Add("The header must include LIC_REGN, LIC_DIST, LIC_TYPE and LIC_SEQN (the ATF licensee list format).");
            return result;
        }

        var now = _clock.UtcNow;
        var existing = await _db.FflDealers.ToDictionaryAsync(d => d.Key, ct);
        var seen = new HashSet<string>();
        var lineNo = 1;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            result.RowsRead++;

            var f = Split(line, delimiter);
            string? Get(int i) => i >= 0 && i < f.Count && !string.IsNullOrWhiteSpace(f[i]) ? f[i].Trim() : null;

            var regn = Digits(Get(c.Regn), 1);
            var dist = Digits(Get(c.Dist), 2);
            var seqn = Digits(Get(c.Seqn), 5);
            var type = Digits(Get(c.Type), 2);
            if (regn is null || dist is null || seqn is null || type is null)
            {
                result.Skipped++;
                if (result.Errors.Count < MaxErrors)
                    result.Errors.Add($"Line {lineNo}: region, district, type and sequence must be numbers.");
                continue;
            }

            var key = FflDirectory.DealerKey(regn, dist, seqn);
            if (!seen.Add(key))
            {
                result.Skipped++;
                continue;
            }

            if (!existing.TryGetValue(key, out var d))
            {
                d = new FflDealer { Key = key };
                _db.FflDealers.Add(d);
                existing[key] = d;
                result.Inserted++;
            }
            else
            {
                result.Updated++;
            }

            d.LicRegn = regn;
            d.LicDist = dist;
            d.LicSeqn = seqn;
            d.LicType = type;
            d.LicCnty = Digits(Get(c.Cnty), 3) ?? d.LicCnty;
            d.LicXprdte = Clip(Get(c.Xprdte), 4) ?? d.LicXprdte;
            d.LicenseName = Clip(Get(c.LicenseName), 255);
            d.BusinessName = Clip(Get(c.BusinessName), 255);
            d.PremiseStreet = Clip(Get(c.PremiseStreet), 200);
            d.PremiseCity = Clip(Get(c.PremiseCity), 100);
            d.PremiseState = Clip(Get(c.PremiseState), 50);
            var newZip = Clip(Get(c.PremiseZip), 20);
            var zipChanged = !string.Equals(FflDirectory.NormalizeZip(newZip), FflDirectory.NormalizeZip(d.PremiseZipCode), StringComparison.Ordinal);
            d.PremiseZipCode = newZip;
            d.MailStreet = Clip(Get(c.MailStreet), 200);
            d.MailCity = Clip(Get(c.MailCity), 100);
            d.MailState = Clip(Get(c.MailState), 50);
            d.MailZipCode = Clip(Get(c.MailZip), 20);
            d.VoicePhone = Clip(Get(c.Phone), 30);

            if (Double(Get(c.Lat)) is double lat && Double(Get(c.Lon)) is double lon && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
            {
                d.Latitude = lat;
                d.Longitude = lon;
            }
            else if (zipChanged)
            {
                // Moved to a new ZIP: the old position is wrong; geocode again.
                d.Latitude = null;
                d.Longitude = null;
            }

            // Dealer data only from columns the file carries (a legacy export), never reset by an ATF refresh.
            if (Get(c.AllowsTransfers) is { } allows)
                d.AllowsTransfers = allows is "1" or "true" or "True" or "TRUE" or "Y" or "Yes" or "yes";
            if (Decimal(Get(c.TransferFee)) is decimal fee && fee >= 0)
                d.LastTransferFee = fee;
            if (Decimal(Get(c.Rating)) is decimal rating && rating is >= 0 and <= 5)
                d.AverageReviewRating = rating;

            d.IsActive = true;
            d.IsManual = false; // a hand-added dealer the ATF list now confirms becomes a normal ATF record
            d.LastImportedOnUtc = now;
        }

        if (request.DeactivateMissing && seen.Count > 0)
        {
            // Hand-added dealers are not in the ATF file by definition; deactivating them would undo the admin's entry.
            foreach (var d in existing.Values.Where(d => d.IsActive && !d.IsManual && !seen.Contains(d.Key)))
            {
                d.IsActive = false;
                result.Deactivated++;
            }
        }

        await _db.SaveChangesAsync(ct);
        result.WithoutGeocode = existing.Values.Count(d => d.IsActive && (d.Latitude is null || d.Longitude is null));

        _logger.LogInformation(
            "FFL directory import: {Rows} rows, {Inserted} inserted, {Updated} updated, {Skipped} skipped, {Deactivated} deactivated.",
            result.RowsRead, result.Inserted, result.Updated, result.Skipped, result.Deactivated);
        return result;
    }

    private sealed record Columns(
        int Regn, int Dist, int Cnty, int Type, int Xprdte, int Seqn,
        int LicenseName, int BusinessName, int PremiseStreet, int PremiseCity, int PremiseState, int PremiseZip,
        int MailStreet, int MailCity, int MailState, int MailZip, int Phone, int Lat, int Lon,
        int AllowsTransfers, int TransferFee, int Rating);

    private static char DetectDelimiter(string header)
    {
        var candidates = new[] { '\t', '|', ',', ';' };
        return candidates.OrderByDescending(ch => header.Count(x => x == ch)).First();
    }

    private static string Normalize(string header) =>
        new string(header.Trim().Trim('"').ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Splits one line, honouring double-quoted fields ("a, b" and "" escapes).</summary>
    internal static List<string> Split(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else sb.Append(ch);
            }
            else if (ch == '"' && sb.Length == 0) quoted = true;
            else if (ch == delimiter) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        fields.Add(sb.ToString());
        return fields;
    }

    /// <summary>Digits only, left-padded with zeros to <paramref name="width"/> (a spreadsheet drops leading zeros).</summary>
    private static string? Digits(string? value, int width)
    {
        if (value is null || value.Length > 10 || !value.All(char.IsDigit))
            return null;
        return value.PadLeft(width, '0');
    }

    private static string? Clip(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];

    private static double? Double(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static decimal? Decimal(string? value) =>
        decimal.TryParse(value?.TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
}
