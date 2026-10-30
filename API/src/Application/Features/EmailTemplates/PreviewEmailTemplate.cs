using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

/// <summary>Renders a template's subject and HTML body using a fixed set of sample token values.</summary>
public record PreviewEmailTemplateQuery(string Key) : IRequest<EmailTemplatePreviewDto>;

public class PreviewEmailTemplateQueryHandler
    : IRequestHandler<PreviewEmailTemplateQuery, EmailTemplatePreviewDto>
{
    private static readonly Regex TokenPattern = new(@"\{\{\s*(\w+)\s*\}\}", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> SampleValues = new Dictionary<string, string>
    {
        ["customerName"] = "Jane Doe",
        ["brandName"] = "HK Parts",
        ["orderNumber"] = "ORD-1001",
        ["orderTotal"] = "$129.00",
        ["trackingNumber"] = "1Z999AA10123456784",
        ["carrier"] = "UPS",
        ["resetUrl"] = "https://example.com/reset",
        ["verificationUrl"] = "https://example.com/verify",
        ["productName"] = "Wireless Headphones",
        ["cartUrl"] = "https://example.com/cart",
    };

    private readonly IApplicationDbContext _db;

    public PreviewEmailTemplateQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<EmailTemplatePreviewDto> Handle(PreviewEmailTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == request.Key, cancellationToken)
            ?? throw new NotFoundException("EmailTemplate", request.Key);

        return new EmailTemplatePreviewDto
        {
            Subject = Render(template.SubjectLine, SampleValues),
            Html = Render(template.HtmlBody, SampleValues),
        };
    }

    /// <summary>Replaces "{{ token }}" placeholders with sample values; unknown tokens render empty.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> values)
        => TokenPattern.Replace(template, m => values.GetValueOrDefault(m.Groups[1].Value, ""));
}
