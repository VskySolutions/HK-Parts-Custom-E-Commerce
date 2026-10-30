using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Discounts;

/// <summary>Soft-deletes a discount rule (idempotent).</summary>
public record DeleteDiscountCommand(Guid Id) : IRequest;

public class DeleteDiscountCommandHandler : IRequestHandler<DeleteDiscountCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteDiscountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteDiscountCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Discounts
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.Discounts.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
