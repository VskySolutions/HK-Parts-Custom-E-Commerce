using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.SmtpAccounts;

public record DeleteSmtpAccountCommand(Guid Id) : IRequest;

public class DeleteSmtpAccountCommandHandler : IRequestHandler<DeleteSmtpAccountCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteSmtpAccountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteSmtpAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _db.SmtpAccounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (account is null)
            return;

        _db.SmtpAccounts.Remove(account);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
