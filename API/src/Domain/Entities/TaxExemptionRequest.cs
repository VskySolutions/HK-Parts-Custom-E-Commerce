using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A customer-submitted request to be treated as tax-exempt (REQ-TAX-003). The request is the workflow;
/// <see cref="Customer.IsTaxExempt"/> remains the effective state that checkout reads — it is only set
/// when an admin approves (AC-TAX-003.3/003.5).
/// </summary>
public class TaxExemptionRequest : AuditableEntity, ISoftDeletable
{
    /// <summary>Owning customer. A plain id (no FK nav) so buyer scoping is by this value.</summary>
    public Guid CustomerId { get; set; }

    public TaxExemptionRequestStatus Status { get; set; } = TaxExemptionRequestStatus.PendingReview;

    /// <summary>Exemption certificate number; at least one of this or <see cref="VatId"/> is required.</summary>
    public string? CertificateNumber { get; set; }
    public string? VatId { get; set; }

    /// <summary>Optional reviewer note, shown to the customer on both approve and reject (AC-TAX-003.4).</summary>
    public string? AdminNote { get; set; }

    public DateTime SubmittedOnUtc { get; set; }
    public DateTime? ReviewedOnUtc { get; set; }

    /// <summary>The reviewing admin's User id.</summary>
    public Guid? ReviewedById { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<TaxExemptionRequestDocument> Documents { get; set; } = new List<TaxExemptionRequestDocument>();
}

/// <summary>
/// Links a <see cref="TaxExemptionRequest"/> to an uploaded supporting document in the central Media
/// table (AC-TAX-003.2). Mirrors the ProductPicture mapping convention — never a raw URL.
/// </summary>
public class TaxExemptionRequestDocument : BaseEntity
{
    public Guid TaxExemptionRequestId { get; set; }
    public TaxExemptionRequest? TaxExemptionRequest { get; set; }

    /// <summary>Media id; a plain id (no FK nav) to avoid a second cascade path into Media.</summary>
    public Guid MediaId { get; set; }
    public int DisplayOrder { get; set; }
}
