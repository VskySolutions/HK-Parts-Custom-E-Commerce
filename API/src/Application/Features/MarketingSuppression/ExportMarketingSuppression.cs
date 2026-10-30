using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;

namespace HKPartsEcommerce.Application.Features.MarketingSuppression;

/// <summary>Exports the entire Marketing Suppression List to CSV (WO-87 AC-ENT-006.5): email, suppressedOnUtc,
/// source. Returns UTF-8 CSV bytes for the admin download endpoint.</summary>
public record ExportMarketingSuppressionQuery : IRequest<byte[]>;

public class ExportMarketingSuppressionQueryHandler : IRequestHandler<ExportMarketingSuppressionQuery, byte[]>
{
    private readonly IApplicationDbContext _db;

    public ExportMarketingSuppressionQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<byte[]> Handle(ExportMarketingSuppressionQuery request, CancellationToken cancellationToken)
    {
        var suppressions = await _db.MarketingSuppressions.AsNoTracking()
            .OrderByDescending(s => s.SuppressedOnUtc)
            .Select(s => new { s.RecipientEmail, s.SuppressedOnUtc, s.Source })
            .ToListAsync(cancellationToken);

        var header = new[] { "Email", "SuppressedOnUtc", "Source" };
        var rows = suppressions.Select(s => new string?[]
        {
            s.RecipientEmail,
            s.SuppressedOnUtc.ToString("O", CultureInfo.InvariantCulture),
            s.Source,
        });

        var csv = Csv.Write(header, rows);
        return Encoding.UTF8.GetBytes(csv);
    }
}
