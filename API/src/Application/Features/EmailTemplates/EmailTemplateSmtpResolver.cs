using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

/// <summary>
/// Resolves the enabled SMTP account that serves a notification category (at most one enabled account
/// per category — see SmtpCategoryRules). Used to route template test-sends and to surface the sender
/// identity in previews (WO-79, AC-ENT-004.2).
/// </summary>
internal static class EmailTemplateSmtpResolver
{
    public static Task<SmtpAccount?> ResolveForCategoryAsync(
        IApplicationDbContext db, NotificationCategory category, CancellationToken cancellationToken)
        => db.SmtpAccounts
            .AsNoTracking()
            .Where(a => a.Enabled && a.Category == category)
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
