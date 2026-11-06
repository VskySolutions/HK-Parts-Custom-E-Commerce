using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A document an admin uploaded for a customer (license, ID...). The file lives in <b>private</b> storage
/// (<see cref="StorageKey"/>, never a public URL) and is only served through the permission-checked admin endpoint.
/// <para>Uploader = <see cref="AuditableEntity.CreatedById"/> (never changes); last editor =
/// <see cref="AuditableEntity.UpdatedById"/>. Soft delete keeps the row and the file for audit and records
/// <see cref="DeletedById"/>. Unlike most soft-deletable entities there is deliberately <b>no</b> global query
/// filter: the admin list shows deleted documents too (muted, read-only).</para>
/// </summary>
public class CustomerDocument : AuditableEntity, ISoftDeletable
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public CustomerDocumentType DocumentType { get; set; }

    /// <summary>Sanitized original file name, shown to admins and used as the download name.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Private storage key (e.g. <c>customer-documents/{customerId}/{guid}.pdf</c>). Never exposed by the API.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>MIME type detected from the file content (not the browser-supplied header).</summary>
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Note { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public Guid? DeletedById { get; set; }
}
