using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.SmtpAccounts;

public record ListSmtpAccountsQuery : IRequest<IReadOnlyList<SmtpAccountDto>>;

public class ListSmtpAccountsQueryHandler
    : IRequestHandler<ListSmtpAccountsQuery, IReadOnlyList<SmtpAccountDto>>
{
    private readonly IApplicationDbContext _db;

    public ListSmtpAccountsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SmtpAccountDto>> Handle(ListSmtpAccountsQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _db.SmtpAccounts
            .AsNoTracking()
            .OrderBy(a => a.DisplayName)
            .ToListAsync(cancellationToken);

        return accounts.Select(SmtpAccountDto.From).ToList();
    }
}
