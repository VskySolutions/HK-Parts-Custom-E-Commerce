using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

public record GetEmailTemplateQuery(string Key) : IRequest<EmailTemplateDto>;

public class GetEmailTemplateQueryHandler : IRequestHandler<GetEmailTemplateQuery, EmailTemplateDto>
{
    private readonly IApplicationDbContext _db;

    public GetEmailTemplateQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<EmailTemplateDto> Handle(GetEmailTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == request.Key, cancellationToken)
            ?? throw new NotFoundException("EmailTemplate", request.Key);

        return EmailTemplateDto.From(template);
    }
}
