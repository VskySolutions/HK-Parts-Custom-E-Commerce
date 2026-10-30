using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.ProductCustomFields;

/// <summary>Soft-deletes a product custom field and drops its product values (idempotent).</summary>
public record DeleteProductCustomFieldCommand(Guid Id) : IRequest;

public class DeleteProductCustomFieldCommandHandler : IRequestHandler<DeleteProductCustomFieldCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteProductCustomFieldCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteProductCustomFieldCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductCustomFields
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        var values = await _db.ProductCustomFieldValues
            .Where(v => v.ProductCustomFieldId == request.Id)
            .ToListAsync(cancellationToken);
        _db.ProductCustomFieldValues.RemoveRange(values);
        _db.ProductCustomFields.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
