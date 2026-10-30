using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>Soft-deletes a category (idempotent).</summary>
public record DeleteCategoryCommand(Guid Id) : IRequest;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.Categories.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
