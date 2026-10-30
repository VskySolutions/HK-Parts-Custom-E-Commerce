using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.AdminUsers;

/// <summary>Updates a user's active status and profile name.</summary>
public record UpdateUserCommand(Guid Id, string FirstName, string LastName, bool IsActive) : IRequest<AdminUserDto>;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).MaximumLength(200);
    }
}

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, AdminUserDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateUserCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<AdminUserDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Customer)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        user.IsActive = request.IsActive;

        if (user.Customer is null)
        {
            user.Customer = new Customer { FirstName = request.FirstName, LastName = request.LastName };
        }
        else
        {
            user.Customer.FirstName = request.FirstName;
            user.Customer.LastName = request.LastName;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return AdminUserDto.From(user);
    }
}
