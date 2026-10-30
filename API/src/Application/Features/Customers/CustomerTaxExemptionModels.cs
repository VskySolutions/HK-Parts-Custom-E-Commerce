using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>A customer's tax-exemption configuration (REQ-TAX-003).</summary>
public class CustomerTaxExemptionDto
{
    public Guid CustomerId { get; set; }
    public bool IsTaxExempt { get; set; }
    public string? CertificateNumber { get; set; }
    public string? VatId { get; set; }

    public static CustomerTaxExemptionDto From(Customer c) => new()
    {
        CustomerId = c.Id,
        IsTaxExempt = c.IsTaxExempt,
        CertificateNumber = c.TaxExemptionCertificate,
        VatId = c.VatId,
    };
}
