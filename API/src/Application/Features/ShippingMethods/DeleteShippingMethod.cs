using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.ShippingMethods;

/// <summary>Soft-deletes a shipping method (idempotent). Cascades to its per-zone rate rows.</summary>
public record DeleteShippingMethodCommand(Guid Id) : IRequest;

public class DeleteShippingMethodCommandHandler : IRequestHandler<DeleteShippingMethodCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteShippingMethodCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteShippingMethodCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ShippingMethods
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.ShippingMethods.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
