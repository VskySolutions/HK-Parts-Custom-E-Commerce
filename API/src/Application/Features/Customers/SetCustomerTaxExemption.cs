using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Marks a customer tax-exempt (or clears it) and captures the exemption certificate / VAT id
/// (AC-TAX-003.1/003.2). An exempt customer is calculated at zero tax at checkout (AC-TAX-003.3).
/// </summary>
public record SetCustomerTaxExemptionCommand(
    Guid CustomerId,
    bool IsTaxExempt,
    string? CertificateNumber = null,
    string? VatId = null) : IRequest<CustomerTaxExemptionDto>;

public class SetCustomerTaxExemptionCommandValidator : AbstractValidator<SetCustomerTaxExemptionCommand>
{
    public SetCustomerTaxExemptionCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CertificateNumber).MaximumLength(200);
        RuleFor(x => x.VatId).MaximumLength(64);
        // An exemption must be substantiated by a certificate number or a VAT id (AC-TAX-003.2).
        RuleFor(x => x)
            .Must(x => !x.IsTaxExempt
                || !string.IsNullOrWhiteSpace(x.CertificateNumber)
                || !string.IsNullOrWhiteSpace(x.VatId))
            .WithMessage("An exempt customer requires a certificate number or a VAT id.")
            .WithName(nameof(SetCustomerTaxExemptionCommand.CertificateNumber));
    }
}

public class SetCustomerTaxExemptionCommandHandler : IRequestHandler<SetCustomerTaxExemptionCommand, CustomerTaxExemptionDto>
{
    private readonly IApplicationDbContext _db;

    public SetCustomerTaxExemptionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerTaxExemptionDto> Handle(SetCustomerTaxExemptionCommand request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        customer.IsTaxExempt = request.IsTaxExempt;
        // Clearing exemption also clears the supporting evidence.
        customer.TaxExemptionCertificate = request.IsTaxExempt ? request.CertificateNumber : null;
        customer.VatId = request.IsTaxExempt ? request.VatId : null;

        await _db.SaveChangesAsync(cancellationToken);
        return CustomerTaxExemptionDto.From(customer);
    }
}
