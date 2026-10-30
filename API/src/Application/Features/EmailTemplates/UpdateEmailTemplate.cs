using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

/// <summary>Updates the editable content of an email template (audited).</summary>
public record UpdateEmailTemplateCommand(
    string Key,
    string Name,
    string SubjectLine,
    string HtmlBody,
    string? PlainTextBody,
    string? Description) : IRequest<EmailTemplateDto>;

public class UpdateEmailTemplateCommandValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    public UpdateEmailTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SubjectLine)
            .NotEmpty()
            .MaximumLength(512)
            .Must(v => FindMalformedToken(v) is null)
            .WithMessage((_, v) => $"Malformed placeholder: '{FindMalformedToken(v)}'");
        RuleFor(x => x.HtmlBody)
            .Must(v => FindMalformedToken(v) is null)
            .WithMessage((_, v) => $"Malformed placeholder: '{FindMalformedToken(v)}'");
        RuleFor(x => x.PlainTextBody)
            .Must(v => FindMalformedToken(v) is null)
            .WithMessage((_, v) => $"Malformed placeholder: '{FindMalformedToken(v)}'");
    }

    /// <summary>
    /// Returns the offending mustache fragment when <paramref name="text"/> contains a malformed
    /// placeholder — an empty "{{}}", an unclosed "{{" (no "}}" before the next "{{" or end), or
    /// unbalanced "{{"/"}}" counts — or null when the tokens are well-formed.
    /// </summary>
    public static string? FindMalformedToken(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        int i = 0;
        while (true)
        {
            int open = text.IndexOf("{{", i, StringComparison.Ordinal);
            if (open < 0)
                break;

            int close = text.IndexOf("}}", open + 2, StringComparison.Ordinal);
            int nextOpen = text.IndexOf("{{", open + 2, StringComparison.Ordinal);

            // Unmatched "{{": no closing "}}", or another "{{" opens before this one closes.
            if (close < 0 || (nextOpen >= 0 && nextOpen < close))
                return text[open..(nextOpen >= 0 ? nextOpen : text.Length)];

            // Empty token: "{{}}" or "{{   }}".
            if (text[(open + 2)..close].Trim().Length == 0)
                return text[open..(close + 2)];

            i = close + 2;
        }

        // Unbalanced counts (e.g. a stray "}}" with no matching "{{").
        if (CountOccurrences(text, "{{") != CountOccurrences(text, "}}"))
            return "}}";

        return null;
    }

    private static int CountOccurrences(string s, string token)
    {
        int count = 0, index = 0;
        while ((index = s.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}

public class UpdateEmailTemplateCommandHandler : IRequestHandler<UpdateEmailTemplateCommand, EmailTemplateDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateEmailTemplateCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<EmailTemplateDto> Handle(UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.EmailTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == request.Key, cancellationToken)
            ?? throw new NotFoundException("EmailTemplate", request.Key);

        template.Name = request.Name;
        template.SubjectLine = request.SubjectLine;
        template.HtmlBody = request.HtmlBody;
        template.PlainTextBody = request.PlainTextBody;
        template.Description = request.Description;

        await _db.SaveChangesAsync(cancellationToken);
        return EmailTemplateDto.From(template);
    }
}
