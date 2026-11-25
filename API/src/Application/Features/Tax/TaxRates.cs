using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Tax;

// PHP parity (TaxController state/zip): the rate tables behind the "By State" and "By ZIP/Postal Code" methods.

public class TaxStateRateDto
{
    public Guid Id { get; set; }
    public string CountryCode { get; set; } = "US";
    public string StateCode { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
    public decimal? RatePercent { get; set; }

    public static TaxStateRateDto From(TaxStateRate r) => new()
    {
        Id = r.Id,
        CountryCode = r.CountryCode,
        StateCode = r.StateCode,
        StateName = r.StateName,
        RatePercent = r.RatePercent,
    };
}

public class TaxZipRateDto
{
    public Guid Id { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }

    public static TaxZipRateDto From(TaxZipRate r) => new() { Id = r.Id, PostalCode = r.PostalCode, RatePercent = r.RatePercent };
}

/// <summary>The US states (+ DC and territories) the state rate grid lists, as the PHP seedTaxState did.</summary>
public static class TaxStates
{
    public static readonly IReadOnlyList<(string Code, string Name)> Us = new[]
    {
        ("AL", "Alabama"), ("AK", "Alaska"), ("AZ", "Arizona"), ("AR", "Arkansas"), ("CA", "California"),
        ("CO", "Colorado"), ("CT", "Connecticut"), ("DE", "Delaware"), ("DC", "District of Columbia"),
        ("FL", "Florida"), ("GA", "Georgia"), ("HI", "Hawaii"), ("ID", "Idaho"), ("IL", "Illinois"),
        ("IN", "Indiana"), ("IA", "Iowa"), ("KS", "Kansas"), ("KY", "Kentucky"), ("LA", "Louisiana"),
        ("ME", "Maine"), ("MD", "Maryland"), ("MA", "Massachusetts"), ("MI", "Michigan"), ("MN", "Minnesota"),
        ("MS", "Mississippi"), ("MO", "Missouri"), ("MT", "Montana"), ("NE", "Nebraska"), ("NV", "Nevada"),
        ("NH", "New Hampshire"), ("NJ", "New Jersey"), ("NM", "New Mexico"), ("NY", "New York"),
        ("NC", "North Carolina"), ("ND", "North Dakota"), ("OH", "Ohio"), ("OK", "Oklahoma"), ("OR", "Oregon"),
        ("PA", "Pennsylvania"), ("RI", "Rhode Island"), ("SC", "South Carolina"), ("SD", "South Dakota"),
        ("TN", "Tennessee"), ("TX", "Texas"), ("UT", "Utah"), ("VT", "Vermont"), ("VA", "Virginia"),
        ("WA", "Washington"), ("WV", "West Virginia"), ("WI", "Wisconsin"), ("WY", "Wyoming"),
        ("AS", "American Samoa"), ("GU", "Guam"), ("MP", "Northern Mariana Islands"), ("PR", "Puerto Rico"),
        ("VI", "U.S. Virgin Islands"), ("AA", "Armed Forces Americas"), ("AE", "Armed Forces Europe"),
        ("AP", "Armed Forces Pacific"),
    };

    /// <summary>Adds any missing state rows (blank rate) so the grid always lists every state.</summary>
    public static async Task EnsureSeededAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existing = await db.TaxStateRates.Where(r => r.CountryCode == "US").Select(r => r.StateCode).ToListAsync(ct);
        var have = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var missing = Us.Where(s => !have.Contains(s.Code)).ToList();
        if (missing.Count == 0)
            return;
        foreach (var (code, name) in missing)
            db.TaxStateRates.Add(new TaxStateRate { CountryCode = "US", StateCode = code, StateName = name });
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>PHP "State Tax Rates": every state with its rate (blank = no tax there).</summary>
public record ListTaxStateRatesQuery : IRequest<List<TaxStateRateDto>>;

public class ListTaxStateRatesQueryHandler : IRequestHandler<ListTaxStateRatesQuery, List<TaxStateRateDto>>
{
    private readonly IApplicationDbContext _db;

    public ListTaxStateRatesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<TaxStateRateDto>> Handle(ListTaxStateRatesQuery request, CancellationToken ct)
    {
        await TaxStates.EnsureSeededAsync(_db, ct);
        var rows = await _db.TaxStateRates.AsNoTracking()
            .OrderBy(r => r.CountryCode).ThenBy(r => r.StateName)
            .ToListAsync(ct);
        return rows.Select(TaxStateRateDto.From).ToList();
    }
}

public record TaxStateRateInput(string StateCode, decimal? RatePercent);

/// <summary>PHP "Save Rates": saves every state's rate at once; a blank rate clears it.</summary>
public record UpdateTaxStateRatesCommand(List<TaxStateRateInput> Rates) : IRequest<List<TaxStateRateDto>>;

public class UpdateTaxStateRatesCommandValidator : AbstractValidator<UpdateTaxStateRatesCommand>
{
    public UpdateTaxStateRatesCommandValidator()
    {
        RuleFor(x => x.Rates).NotNull();
        RuleForEach(x => x.Rates).ChildRules(r =>
        {
            r.RuleFor(x => x.StateCode).NotEmpty().MaximumLength(3);
            r.RuleFor(x => x.RatePercent).InclusiveBetween(0m, 100m).When(x => x.RatePercent.HasValue)
                .WithMessage("A state tax rate must be between 0 and 100 percent.");
        });
    }
}

public class UpdateTaxStateRatesCommandHandler : IRequestHandler<UpdateTaxStateRatesCommand, List<TaxStateRateDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public UpdateTaxStateRatesCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<List<TaxStateRateDto>> Handle(UpdateTaxStateRatesCommand request, CancellationToken ct)
    {
        await TaxStates.EnsureSeededAsync(_db, ct);
        var rows = await _db.TaxStateRates.Where(r => r.CountryCode == "US").ToListAsync(ct);
        var byCode = rows.ToDictionary(r => r.StateCode, StringComparer.OrdinalIgnoreCase);
        foreach (var input in request.Rates)
        {
            if (byCode.TryGetValue(input.StateCode.Trim(), out var row))
                row.RatePercent = input.RatePercent is decimal r && r > 0m ? Math.Round(r, 4) : null;
        }
        await _db.SaveChangesAsync(ct);
        return await _mediator.Send(new ListTaxStateRatesQuery(), ct);
    }
}

/// <summary>PHP "Tax By ZIP/Postal Code" listing (50 per page) with its ZIP search.</summary>
public record ListTaxZipRatesQuery(string? Search = null, int Page = 1, int PageSize = 50) : IRequest<PaginatedList<TaxZipRateDto>>;

public class ListTaxZipRatesQueryHandler : IRequestHandler<ListTaxZipRatesQuery, PaginatedList<TaxZipRateDto>>
{
    private readonly IApplicationDbContext _db;

    public ListTaxZipRatesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<TaxZipRateDto>> Handle(ListTaxZipRatesQuery request, CancellationToken ct)
    {
        var query = _db.TaxZipRates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(r => r.PostalCode.StartsWith(term));
        }
        var page = await PaginatedList<TaxZipRate>.CreateAsync(
            query.OrderBy(r => r.PostalCode), Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 500), ct);
        return new PaginatedList<TaxZipRateDto>(
            page.Items.Select(TaxZipRateDto.From).ToList(), page.TotalCount, page.PageNumber, page.PageSize);
    }
}

/// <summary>Adds (Id null) or edits a ZIP rate. A ZIP can only be on file once.</summary>
public record SaveTaxZipRateCommand(Guid? Id, string PostalCode, decimal RatePercent) : IRequest<TaxZipRateDto>;

public class SaveTaxZipRateCommandValidator : AbstractValidator<SaveTaxZipRateCommand>
{
    public SaveTaxZipRateCommandValidator()
    {
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20).WithName("ZIP/Postal Code");
        RuleFor(x => x.RatePercent).InclusiveBetween(0m, 100m).WithName("Rate");
    }
}

public class SaveTaxZipRateCommandHandler : IRequestHandler<SaveTaxZipRateCommand, TaxZipRateDto>
{
    private readonly IApplicationDbContext _db;

    public SaveTaxZipRateCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TaxZipRateDto> Handle(SaveTaxZipRateCommand request, CancellationToken ct)
    {
        var zip = TaxZipRateRules.Normalize(request.PostalCode);
        if (await _db.TaxZipRates.AnyAsync(r => r.PostalCode == zip && r.Id != request.Id, ct))
            throw new ConflictException($"A tax rate for ZIP/postal code {zip} already exists.");

        TaxZipRate row;
        if (request.Id is Guid id)
        {
            row = await _db.TaxZipRates.FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException(nameof(TaxZipRate), id);
        }
        else
        {
            row = new TaxZipRate();
            _db.TaxZipRates.Add(row);
        }
        row.PostalCode = zip;
        row.RatePercent = Math.Round(request.RatePercent, 4);
        await _db.SaveChangesAsync(ct);
        return TaxZipRateDto.From(row);
    }
}

public record DeleteTaxZipRateCommand(Guid Id) : IRequest;

public class DeleteTaxZipRateCommandHandler : IRequestHandler<DeleteTaxZipRateCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteTaxZipRateCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteTaxZipRateCommand request, CancellationToken ct)
    {
        var row = await _db.TaxZipRates.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(TaxZipRate), request.Id);
        _db.TaxZipRates.Remove(row);
        await _db.SaveChangesAsync(ct);
    }
}

public record TaxZipImportResult(int Added, int Updated, int Skipped, List<string> Errors);

/// <summary>
/// Bulk-loads ZIP rates from CSV (<c>zip,rate</c>; a header row is optional): new ZIPs are added, existing ones get
/// the new rate. Not in PHP, but entering thousands of ZIPs one at a time is not workable.
/// </summary>
public record ImportTaxZipRatesCommand(string CsvText) : IRequest<TaxZipImportResult>;

public class ImportTaxZipRatesCommandHandler : IRequestHandler<ImportTaxZipRatesCommand, TaxZipImportResult>
{
    private const int MaxErrors = 50;
    private readonly IApplicationDbContext _db;

    public ImportTaxZipRatesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TaxZipImportResult> Handle(ImportTaxZipRatesCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CsvText))
            throw new Common.Exceptions.ValidationException(new[] { new ValidationFailure("file", "The file is empty.") });

        var records = Csv.Parse(request.CsvText);
        var errors = new List<string>();
        var parsed = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var zipText = record.Count > 0 ? record[0].Trim() : string.Empty;
            var rateText = record.Count > 1 ? record[1].Trim().TrimEnd('%') : string.Empty;
            if (i == 0 && !decimal.TryParse(rateText, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                continue; // header row
            if (zipText.Length == 0 || zipText.Length > 20
                || !decimal.TryParse(rateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
                || rate < 0m || rate > 100m)
            {
                skipped++;
                if (errors.Count < MaxErrors)
                    errors.Add($"Row {i + 1}: expected a ZIP/postal code and a rate between 0 and 100 (got \"{string.Join(",", record)}\").");
                continue;
            }
            parsed[TaxZipRateRules.Normalize(zipText)] = Math.Round(rate, 4);
        }

        var zips = parsed.Keys.ToList();
        var existing = new Dictionary<string, TaxZipRate>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in zips.Chunk(1000))
        {
            var rows = await _db.TaxZipRates.Where(r => chunk.Contains(r.PostalCode)).ToListAsync(ct);
            foreach (var r in rows)
                existing[r.PostalCode] = r;
        }

        int added = 0, updated = 0;
        foreach (var (zip, rate) in parsed)
        {
            if (existing.TryGetValue(zip, out var row))
            {
                if (row.RatePercent != rate)
                {
                    row.RatePercent = rate;
                    updated++;
                }
            }
            else
            {
                _db.TaxZipRates.Add(new TaxZipRate { PostalCode = zip, RatePercent = rate });
                added++;
            }
        }
        await _db.SaveChangesAsync(ct);
        return new TaxZipImportResult(added, updated, skipped, errors);
    }
}

public static class TaxZipRateRules
{
    /// <summary>Stored trimmed and upper-cased; checkout matches the first 5 characters of the ship-to ZIP against it.</summary>
    public static string Normalize(string postalCode) => postalCode.Trim().ToUpperInvariant();
}
