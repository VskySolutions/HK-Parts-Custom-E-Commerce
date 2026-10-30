using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerAddresses;

/// <summary>Updates one of the authenticated customer's addresses (ownership enforced; AC-CUS-002.2).</summary>
public record UpdateAddressCommand(
    Guid Id,
    AddressType AddressType,
    bool IsDefault,
    string FirstName,
    string LastName,
    string? Company,
    string AddressLine1,
    string? AddressLine2,
    string? Landmark,
    string City,
    string? StateProvince,
    string PostalCode,
    string CountryCode,
    string? PhoneNumber) : IRequest<AddressDto>;

public class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AddressType).IsInEnum();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Company).MaximumLength(200);
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(255);
        RuleFor(x => x.AddressLine2).MaximumLength(255);
        RuleFor(x => x.Landmark).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(120);
        RuleFor(x => x.StateProvince).NotEmpty().MaximumLength(120);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
    }
}

public class UpdateAddressCommandHandler : IRequestHandler<UpdateAddressCommand, AddressDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public UpdateAddressCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<AddressDto> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .Where(c => c.UserId == _current.UserId.Value)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var mapping = await _db.CustomerAddresses
            .Include(m => m.Address)
            .FirstOrDefaultAsync(m => m.Id == request.Id && m.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerAddress), request.Id);

        // A type must always have a default; if this is the only address of its (possibly new) type,
        // keep it the default automatically (AC-CUS-002.3).
        var hasOtherOfType = await _db.CustomerAddresses.AnyAsync(
            m => m.CustomerId == customerId && m.AddressType == request.AddressType && m.Id != mapping.Id,
            cancellationToken);
        var makeDefault = request.IsDefault || !hasOtherOfType;

        if (makeDefault)
            await ClearExistingDefaultsAsync(customerId, request.AddressType, mapping.Id, cancellationToken);

        mapping.AddressType = request.AddressType;
        mapping.IsDefault = makeDefault;

        var address = mapping.Address!;
        address.FirstName = request.FirstName.Trim();
        address.LastName = request.LastName.Trim();
        address.Company = request.Company;
        address.AddressLine1 = request.AddressLine1.Trim();
        address.AddressLine2 = request.AddressLine2;
        address.Landmark = request.Landmark;
        address.City = request.City.Trim();
        address.StateProvince = request.StateProvince;
        address.PostalCode = request.PostalCode.Trim();
        address.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        address.PhoneNumber = request.PhoneNumber;

        await _db.SaveChangesAsync(cancellationToken);

        return AddressDto.From(mapping);
    }

    // Clears (and persists) any other default of the same type before this one becomes the default, so
    // the filtered unique index (one default per customer+type) is never transiently violated.
    private async Task ClearExistingDefaultsAsync(
        Guid customerId, AddressType type, Guid excludeId, CancellationToken cancellationToken)
    {
        var currentDefaults = await _db.CustomerAddresses
            .Where(m => m.CustomerId == customerId && m.AddressType == type && m.Id != excludeId && m.IsDefault)
            .ToListAsync(cancellationToken);

        if (currentDefaults.Count == 0)
            return;

        foreach (var m in currentDefaults)
            m.IsDefault = false;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
