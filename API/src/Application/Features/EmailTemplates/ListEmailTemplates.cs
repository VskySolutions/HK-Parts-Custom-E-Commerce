using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

public record ListEmailTemplatesQuery(NotificationCategory? Category = null, bool? Enabled = null, string? Search = null)
    : IRequest<IReadOnlyList<EmailTemplateSummaryDto>>;

public class ListEmailTemplatesQueryHandler
    : IRequestHandler<ListEmailTemplatesQuery, IReadOnlyList<EmailTemplateSummaryDto>>
{
    private readonly IApplicationDbContext _db;

    public ListEmailTemplatesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<EmailTemplateSummaryDto>> Handle(ListEmailTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.EmailTemplates.AsNoTracking();

        if (request.Category is { } category)
            query = query.Where(t => t.Category == category);
        if (request.Enabled is { } enabled)
            query = query.Where(t => t.Enabled == enabled);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(term) || t.TemplateKey.ToLower().Contains(term));
        }

        var templates = await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.TemplateKey)
            .ToListAsync(cancellationToken);

        // Map each notification category to its enabled SMTP account (at most one per category).
        var smtpByCategory = (await _db.SmtpAccounts
                .AsNoTracking()
                .Where(a => a.Enabled)
                .ToListAsync(cancellationToken))
            .Where(a => a.Category is not null)
            .GroupBy(a => a.Category!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return templates.Select(t =>
        {
            var dto = EmailTemplateSummaryDto.From(t);
            if (smtpByCategory.TryGetValue(t.Category, out var account))
            {
                dto.HasSmtpConfigured = true;
                dto.AssignedSmtpAccountName = account.DisplayName;
            }
            return dto;
        }).ToList();
    }
}
