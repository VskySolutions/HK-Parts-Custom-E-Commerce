using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

/// <summary>Enables or disables a template; disabling a critical template requires confirmation.</summary>
public record SetEmailTemplateEnabledCommand(string Key, bool Enabled, bool Confirm = false)
    : IRequest<EmailTemplateDto>;

public class SetEmailTemplateEnabledCommandHandler
    : IRequestHandler<SetEmailTemplateEnabledCommand, EmailTemplateDto>
{
    private readonly IApplicationDbContext _db;

    public SetEmailTemplateEnabledCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<EmailTemplateDto> Handle(SetEmailTemplateEnabledCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.EmailTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == request.Key, cancellationToken)
            ?? throw new NotFoundException("EmailTemplate", request.Key);

        if (template.IsCritical && !request.Enabled && !request.Confirm)
            throw new ConflictException("Disabling a critical template requires explicit confirmation.");

        template.Enabled = request.Enabled;

        await _db.SaveChangesAsync(cancellationToken);
        return EmailTemplateDto.From(template);
    }
}
