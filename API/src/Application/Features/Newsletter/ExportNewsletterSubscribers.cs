using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;

namespace HKPartsEcommerce.Application.Features.Newsletter;

/// <summary>Exports every newsletter subscriber to CSV (WO-56): email, status, source, createdOnUtc,
/// isSuppressed. Returns the UTF-8 CSV bytes for the admin download endpoint.</summary>
public record ExportNewsletterSubscribersQuery : IRequest<byte[]>;

public class ExportNewsletterSubscribersQueryHandler : IRequestHandler<ExportNewsletterSubscribersQuery, byte[]>
{
    private readonly IApplicationDbContext _db;

    public ExportNewsletterSubscribersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<byte[]> Handle(ExportNewsletterSubscribersQuery request, CancellationToken cancellationToken)
    {
        var subscribers = await _db.CMSNewsletterSubscriptions.AsNoTracking()
            .OrderByDescending(s => s.CreatedOnUtc)
            .Select(s => new { s.Email, s.Status, s.Source, s.CreatedOnUtc })
            .ToListAsync(cancellationToken);

        // Whole-list suppression lookup (admin export is inherently full-table).
        var suppressedSet = new HashSet<string>(
            await _db.MarketingSuppressions.AsNoTracking()
                .Select(m => m.RecipientEmail)
                .ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var header = new[] { "Email", "Status", "Source", "CreatedOnUtc", "IsSuppressed" };
        var rows = subscribers.Select(s => new string?[]
        {
            s.Email,
            s.Status.ToString(),
            s.Source,
            s.CreatedOnUtc.ToString("O", CultureInfo.InvariantCulture),
            suppressedSet.Contains(s.Email) ? "true" : "false",
        });

        var csv = Csv.Write(header, rows);
        return Encoding.UTF8.GetBytes(csv);
    }
}
