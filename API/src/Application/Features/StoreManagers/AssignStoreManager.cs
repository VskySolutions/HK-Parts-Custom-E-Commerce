using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.StoreManagers;

/// <summary>
/// Assigns a user as manager of a store. One assignment per user — if the user already manages a
/// store, the existing assignment is repointed to the new store (Store Management REQ-STR-004).
/// </summary>
public record AssignStoreManagerCommand(Guid UserId, Guid StoreId) : IRequest<StoreManagerAssignmentDto>;

public class AssignStoreManagerCommandValidator : AbstractValidator<AssignStoreManagerCommand>
{
    public AssignStoreManagerCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.StoreId).NotEmpty();
    }
}

public class AssignStoreManagerCommandHandler : IRequestHandler<AssignStoreManagerCommand, StoreManagerAssignmentDto>
{
    private readonly IApplicationDbContext _db;

    public AssignStoreManagerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<StoreManagerAssignmentDto> Handle(AssignStoreManagerCommand request, CancellationToken cancellationToken)
    {
        var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == request.UserId, cancellationToken);
        if (!userExists)
            throw new NotFoundException(nameof(User), request.UserId);

        var storeExists = await _db.Stores.AsNoTracking().AnyAsync(s => s.Id == request.StoreId, cancellationToken);
        if (!storeExists)
            throw new NotFoundException(nameof(Store), request.StoreId);

        var assignment = await _db.StoreManagerAssignments
            .FirstOrDefaultAsync(a => a.UserId == request.UserId, cancellationToken);

        if (assignment is null)
        {
            assignment = new StoreManagerAssignment
            {
                UserId = request.UserId,
                StoreId = request.StoreId,
            };
            _db.StoreManagerAssignments.Add(assignment);
        }
        else
        {
            assignment.StoreId = request.StoreId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var saved = await _db.StoreManagerAssignments
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Store)
            .FirstAsync(a => a.Id == assignment.Id, cancellationToken);

        return StoreManagerAssignmentDto.From(saved);
    }
}
