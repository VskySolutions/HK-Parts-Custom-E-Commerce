using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.EmailLog;

/// <summary>Returns a single email's full record (including the rendered body and last error).</summary>
public record GetEmailLogQuery(Guid Id) : IRequest<EmailLogDetailDto>;

public class GetEmailLogQueryHandler : IRequestHandler<GetEmailLogQuery, EmailLogDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetEmailLogQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<EmailLogDetailDto> Handle(GetEmailLogQuery request, CancellationToken cancellationToken)
    {
        var email = await _db.EmailQueue
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmailQueue), request.Id);

        return EmailLogDetailDto.From(email);
    }
}
