using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Soft-deletes a product (idempotent).</summary>
public record DeleteProductCommand(Guid Id) : IRequest;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteProductCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.Products.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
