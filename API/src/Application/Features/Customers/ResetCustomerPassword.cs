using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Admin sets a new password for a customer (Customer edit page → "Reset password"). Kept separate from the
/// profile update on purpose. The password is validated with the shared <see cref="PasswordRules"/> policy and
/// stored only as a hash via <see cref="IPasswordHasher"/> (same as registration / <c>SetUserPassword</c>).
/// After the change every active refresh token is revoked (signs the customer out everywhere, like logout-all)
/// and any unused emailed reset links are consumed so they can't override the new password.
/// Passwords are never logged or echoed back.
/// </summary>
public record ResetCustomerPasswordCommand(Guid CustomerId, string NewPassword, string ConfirmPassword) : IRequest;

public class ResetCustomerPasswordCommandValidator : AbstractValidator<ResetCustomerPasswordCommand>
{
    public ResetCustomerPasswordCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.NewPassword).Password();
        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Please confirm the new password.")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public class ResetCustomerPasswordCommandHandler : IRequestHandler<ResetCustomerPasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTimeProvider _clock;

    public ResetCustomerPasswordCommandHandler(IApplicationDbContext db, IPasswordHasher hasher, IDateTimeProvider clock)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task Handle(ResetCustomerPasswordCommand request, CancellationToken cancellationToken)
    {
        // Soft-deleted customers are hidden by the global filter → 404.
        var customer = await _db.Customers
            .Include(c => c.User!)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var user = customer.User ?? throw new NotFoundException(nameof(User), request.CustomerId);

        // Staff accounts also carry a Customer profile; their passwords are managed from Users, not here.
        var customerRole = nameof(RoleType.Customer);
        if (user.UserRoles.Any(ur => ur.Role != null && ur.Role.Name != customerRole))
            throw new ConflictException("This account belongs to a staff user. Reset its password from Users.");

        var now = _clock.UtcNow;
        user.PasswordHash = _hasher.Hash(request.NewPassword);

        // End existing sessions (same as logout-all).
        var sessions = await _db.RefreshTokens
            .Where(r => r.UserId == user.Id && r.RevokedOnUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in sessions)
            token.RevokedOnUtc = now;

        // Retire unused "forgot password" links issued before this reset.
        var resetLinks = await _db.UserTokens
            .Where(t => t.UserId == user.Id && t.Purpose == UserTokenPurpose.PasswordReset && t.ConsumedOnUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var link in resetLinks)
            link.ConsumedOnUtc = now;

        // Single save: hash + revocations commit together, or not at all.
        await _db.SaveChangesAsync(cancellationToken);
    }
}
