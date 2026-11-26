using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

public record ListEmailTemplatesQuery(NotificationCategory? Category = null, bool? Enabled = null, int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool SortDescending = false) : IRequest<PaginatedList<EmailTemplateSummaryDto>>;

public class ListEmailTemplatesQueryHandler : IRequestHandler<ListEmailTemplatesQuery, PaginatedList<EmailTemplateSummaryDto>>
{
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["templateKey"] = "TemplateKey",
        ["category"] = "Category",
        ["enabled"] = "Enabled"
    };

    private readonly IApplicationDbContext _db;

    public ListEmailTemplatesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<EmailTemplateSummaryDto>> Handle(ListEmailTemplatesQuery request, CancellationToken cancellationToken)
    {
        //var query = _db.EmailTemplates.AsNoTracking();

        //if (request.Category is { } category)
        //    query = query.Where(t => t.Category == category);
        //if (request.Enabled is { } enabled)
        //    query = query.Where(t => t.Enabled == enabled);
        //if (!string.IsNullOrWhiteSpace(request.Search))
        //{
        //    var term = request.Search.Trim().ToLower();
        //    query = query.Where(t => t.Name.ToLower().Contains(term) || t.TemplateKey.ToLower().Contains(term));
        //}

        //var templates = await query
        //    .OrderBy(t => t.Category)
        //    .ThenBy(t => t.TemplateKey)
        //    .ToListAsync(cancellationToken);

        //// Map each notification category to its enabled SMTP account (at most one per category).
        //var smtpByCategory = (await _db.SmtpAccounts
        //        .AsNoTracking()
        //        .Where(a => a.Enabled)
        //        .ToListAsync(cancellationToken))
        //    .Where(a => a.Category is not null)
        //    .GroupBy(a => a.Category!.Value)
        //    .ToDictionary(g => g.Key, g => g.First());

        //return templates.Select(t =>
        //{
        //    var dto = EmailTemplateSummaryDto.From(t);
        //    if (smtpByCategory.TryGetValue(t.Category, out var account))
        //    {
        //        dto.HasSmtpConfigured = true;
        //        dto.AssignedSmtpAccountName = account.DisplayName;
        //    }
        //    return dto;
        //}).ToList();

        IQueryable<EmailTemplate> query = _db.EmailTemplates.AsNoTracking();

        if (request.Category.HasValue)
        {
            query = query.Where(t => t.Category == request.Category.Value);
        }

        if (request.Enabled.HasValue)
        {
            query = query.Where(t => t.Enabled == request.Enabled.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            query = query.Where(t =>
                t.Name.Contains(term) ||
                t.TemplateKey.Contains(term));
        }

        // Sorting
        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(t => t.Category).ThenBy(t => t.TemplateKey));

        // Pagination
        var page = await PaginatedList<EmailTemplate>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);

        // Get SMTP accounts only for categories on the current page
        var categories = page.Items.Select(t => t.Category).Distinct().ToList();

        var smtpAccounts = await _db.SmtpAccounts
            .AsNoTracking()
            .Where(a =>
                a.Enabled &&
                a.Category.HasValue &&
                categories.Contains(a.Category.Value))
            .ToListAsync(cancellationToken);

        var smtpByCategory = smtpAccounts
            .Where(a => a.Category.HasValue)
            .GroupBy(a => a.Category!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // Mapping
        var items = page.Items.Select(t =>
        {
            var dto = EmailTemplateSummaryDto.From(t);

            if (smtpByCategory.TryGetValue(t.Category, out var account))
            {
                dto.HasSmtpConfigured = true;
                dto.AssignedSmtpAccountName = account.DisplayName;
            }

            return dto;
        }).ToList();

        return new PaginatedList<EmailTemplateSummaryDto>(items, page.TotalCount, page.PageNumber,page.PageSize);
    }
    
}
