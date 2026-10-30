using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.AdminUsers;

/// <summary>Returns a single user by id, including profile and role assignments.</summary>
public record GetUserQuery(Guid Id) : IRequest<AdminUserDto>;

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, AdminUserDto>
{
    private readonly IApplicationDbContext _db;

    public GetUserQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<AdminUserDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Customer)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.Id);

        return AdminUserDto.From(user);
    }
}
