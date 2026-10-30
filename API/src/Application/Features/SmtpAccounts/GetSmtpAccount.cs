using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.SmtpAccounts;

public record GetSmtpAccountQuery(Guid Id) : IRequest<SmtpAccountDto>;

public class GetSmtpAccountQueryHandler : IRequestHandler<GetSmtpAccountQuery, SmtpAccountDto>
{
    private readonly IApplicationDbContext _db;

    public GetSmtpAccountQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<SmtpAccountDto> Handle(GetSmtpAccountQuery request, CancellationToken cancellationToken)
    {
        var account = await _db.SmtpAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("SmtpAccount", request.Id);

        return SmtpAccountDto.From(account);
    }
}
