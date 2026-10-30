using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

/// <summary>Removes a currency (idempotent). The base currency cannot be deleted.</summary>
public record DeleteCurrencyCommand(string Code) : IRequest;

public class DeleteCurrencyCommandHandler : IRequestHandler<DeleteCurrencyCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCurrencyCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.ToUpper();

        var entity = await _db.SupportedCurrencies
            .FirstOrDefaultAsync(c => c.CurrencyCode.ToUpper() == code, cancellationToken);

        if (entity is null)
            return;

        if (entity.IsBaseCurrency)
            throw new ConflictException("The base currency cannot be deleted.");

        _db.SupportedCurrencies.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
