using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.GiftCertificates;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Nightly integrity check: for every certificate, CurrentBalance must equal OriginalBalance + Σ(ledger
/// entries after Created). Any mismatch is logged (code masked) and raised as an admin alert; nothing is
/// "fixed" automatically — a mismatch means a change bypassed the ledger and needs a human.
/// </summary>
public class GiftCertificateReconciliationWorker : IScheduledTask
{
    public string Name => "GiftCertificateReconciliationJob";
    public TimeSpan DefaultInterval => TimeSpan.FromHours(24);
    public string? IntervalSettingKey => null;
    public string? CronSettingKey => "tasks.gift-certificate-reconciliation.cron";

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);

        var mismatches = await db.GiftCertificates.IgnoreQueryFilters().AsNoTracking()
            .Select(g => new
            {
                g.Code,
                g.OriginalBalance,
                g.CurrentBalance,
                Ledger = g.Transactions.Where(t => t.Action != GiftCertificateAction.Created).Sum(t => (decimal?)t.Amount) ?? 0m,
            })
            .Where(x => x.OriginalBalance + x.Ledger != x.CurrentBalance)
            .ToListAsync(cancellationToken);

        foreach (var m in mismatches)
            logger.LogError(
                "Gift certificate {Code} ledger mismatch: original {Original} + ledger {Ledger} != current {Current}.",
                GiftCertificateRules.Mask(m.Code), m.OriginalBalance, m.Ledger, m.CurrentBalance);

        if (mismatches.Count > 0)
            await services.GetRequiredService<IAdminAlertService>().RaiseAsync(
                "GiftCertificateLedgerMismatch",
                $"{mismatches.Count} gift certificate(s) do not reconcile with their ledger",
                string.Join(", ", mismatches.Take(20).Select(m => GiftCertificateRules.Mask(m.Code))),
                "Error", Name, cancellationToken);
        else
            logger.LogInformation("{Task}: all gift certificates reconcile with their ledger.", Name);
    }
}
