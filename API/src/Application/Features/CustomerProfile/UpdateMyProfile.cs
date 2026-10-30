using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CustomerProfile;

/// <summary>Updates the authenticated customer's own name and phone number (AC-CUS-002.1).</summary>
public record UpdateMyProfileCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? PreferredTimeZone = null) : IRequest<CustomerProfileDto>;

public class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).MaximumLength(50);
        RuleFor(x => x.PreferredTimeZone).MaximumLength(64);
    }
}

public class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, CustomerProfileDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public UpdateMyProfileCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CustomerProfileDto> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customer = await _db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == _current.UserId.Value, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        customer.PreferredTimeZone = string.IsNullOrWhiteSpace(request.PreferredTimeZone) ? null : request.PreferredTimeZone.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return CustomerProfileDto.From(customer, customer.User!);
    }
}
