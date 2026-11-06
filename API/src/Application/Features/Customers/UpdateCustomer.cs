using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Admin edits an existing customer's profile: first/last name and phone number.
/// Email is the sign-in identity and is intentionally <b>not</b> part of this command — it is read-only
/// for admins. Everything else on the customer (activation, pricing group, tax exemption, WhatsApp
/// number/opt-in, addresses, store credit) is left untouched. Returns the refreshed admin detail.
/// </summary>
public record UpdateCustomerCommand(
    Guid CustomerId,
    string FirstName,
    string? LastName,
    string? PhoneNumber,
    string? LongText = null,
    bool? AllowRestrictedCapacityPurchases = null) : IRequest<CustomerDetailDto>;

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    /// <summary>Digits plus the usual separators (+, spaces, dashes, dots, parentheses).</summary>
    internal const string PhonePattern = @"^\+?[0-9\s\-\.\(\)]{5,50}$";

    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).MaximumLength(200);
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50)
            .Matches(PhonePattern).WithMessage("Phone number may contain only digits, spaces and + - . ( ).")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.LongText).MaximumLength(4000);
    }
}

public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, CustomerDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public UpdateCustomerCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<CustomerDetailDto> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        // Only the permitted profile fields; nulls/blank optional values clear the field.
        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName?.Trim() ?? string.Empty;
        customer.PhoneNumber = Normalize(request.PhoneNumber);
        // Custom information. LongText follows the form (blank clears it); the flag is only changed when sent.
        customer.LongText = Normalize(request.LongText);
        if (request.AllowRestrictedCapacityPurchases is bool allow)
            customer.AllowRestrictedCapacityPurchases = allow;

        await _db.SaveChangesAsync(cancellationToken);

        return await _mediator.Send(new GetCustomerDetailQuery(request.CustomerId), cancellationToken);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
