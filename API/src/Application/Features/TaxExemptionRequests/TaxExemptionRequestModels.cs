using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.TaxExemptionRequests;

/// <summary>A supporting document attached to a request, with its Media URL for viewing.</summary>
public class TaxExemptionDocumentDto
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public string? FileName { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>A tax exemption request (REQ-TAX-003) for both the customer portal and the admin queue.</summary>
public class TaxExemptionRequestDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public TaxExemptionRequestStatus Status { get; set; }
    public string? CertificateNumber { get; set; }
    public string? VatId { get; set; }
    public string? AdminNote { get; set; }
    public DateTime SubmittedOnUtc { get; set; }
    public DateTime? ReviewedOnUtc { get; set; }

    /// <summary>Customer identity, populated for the admin queue only.</summary>
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }

    public List<TaxExemptionDocumentDto> Documents { get; set; } = new();

    public static TaxExemptionRequestDto From(TaxExemptionRequest r) => new()
    {
        Id = r.Id,
        CustomerId = r.CustomerId,
        Status = r.Status,
        CertificateNumber = r.CertificateNumber,
        VatId = r.VatId,
        AdminNote = r.AdminNote,
        SubmittedOnUtc = r.SubmittedOnUtc,
        ReviewedOnUtc = r.ReviewedOnUtc,
        Documents = r.Documents
            .OrderBy(d => d.DisplayOrder)
            .Select(d => new TaxExemptionDocumentDto
            {
                Id = d.Id,
                MediaId = d.MediaId,
                DisplayOrder = d.DisplayOrder,
            })
            .ToList(),
    };
}

/// <summary>
/// The customer portal's view of their exemption state (AC-TAX-003.3): the effective flag plus the latest
/// request, if any. <see cref="Status"/> is "NotSubmitted" when they have never submitted one.
/// </summary>
public class MyTaxExemptionDto
{
    /// <summary>NotSubmitted | PendingReview | Approved | Rejected.</summary>
    public string Status { get; set; } = "NotSubmitted";

    /// <summary>The effective exemption flag — only ever true once a request is approved.</summary>
    public bool IsTaxExempt { get; set; }

    /// <summary>True when no request is awaiting review, so the customer may submit one (AC-TAX-003.6).</summary>
    public bool CanSubmit { get; set; }

    public TaxExemptionRequestDto? LatestRequest { get; set; }
}
