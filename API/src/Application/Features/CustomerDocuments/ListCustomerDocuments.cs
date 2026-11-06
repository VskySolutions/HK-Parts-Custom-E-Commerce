using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerDocuments;

/// <summary>One customer document row. Never carries the storage key or any URL to the stored file.</summary>
public class CustomerDocumentDto
{
    public Guid Id { get; set; }
    public CustomerDocumentType DocumentType { get; set; }
    public string DocumentTypeLabel { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Note { get; set; }
    /// <summary>True for PDF/images (open in a tab); false for Word files (download).</summary>
    public bool IsInline { get; set; }
    /// <summary>"Active" or "Deleted".</summary>
    public string Status { get; set; } = CustomerDocumentStatus.Active;

    public DateTime UploadedOnUtc { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public string? DeletedBy { get; set; }
}

public static class CustomerDocumentStatus
{
    public const string Active = "Active";
    public const string Deleted = "Deleted";
}

/// <summary>The Documents tab payload: every document (newest first) plus the active count for the tab badge.</summary>
public class CustomerDocumentListDto
{
    public List<CustomerDocumentDto> Items { get; set; } = new();
    public int ActiveCount { get; set; }
}

/// <summary>Document-type dropdown option.</summary>
public record CustomerDocumentTypeOption(CustomerDocumentType Value, string Label);

/// <summary>Lists a customer's documents, active and deleted (WO Documents tab).</summary>
public record ListCustomerDocumentsQuery(Guid CustomerId) : IRequest<CustomerDocumentListDto>;

/// <summary>The available document types for the upload/edit dropdown.</summary>
public record ListCustomerDocumentTypesQuery : IRequest<IReadOnlyList<CustomerDocumentTypeOption>>;

public class ListCustomerDocumentTypesQueryHandler : IRequestHandler<ListCustomerDocumentTypesQuery, IReadOnlyList<CustomerDocumentTypeOption>>
{
    public Task<IReadOnlyList<CustomerDocumentTypeOption>> Handle(ListCustomerDocumentTypesQuery request, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CustomerDocumentTypeOption>>(
            CustomerDocumentTypes.All.Select(t => new CustomerDocumentTypeOption(t.Value, t.Label)).ToList());
}

public class ListCustomerDocumentsQueryHandler : IRequestHandler<ListCustomerDocumentsQuery, CustomerDocumentListDto>
{
    private readonly IApplicationDbContext _db;

    public ListCustomerDocumentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerDocumentListDto> Handle(ListCustomerDocumentsQuery request, CancellationToken cancellationToken)
    {
        await CustomerDocumentMapper.EnsureCustomerAsync(_db, request.CustomerId, cancellationToken);

        var docs = await _db.CustomerDocuments.AsNoTracking()
            .Where(d => d.CustomerId == request.CustomerId)
            .OrderByDescending(d => d.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return new CustomerDocumentListDto
        {
            Items = await CustomerDocumentMapper.ToDtosAsync(_db, docs, cancellationToken),
            ActiveCount = docs.Count(d => !d.Deleted),
        };
    }
}

/// <summary>Shared lookups/mapping for the customer-document handlers.</summary>
internal static class CustomerDocumentMapper
{
    /// <summary>404 unless the customer exists and is not soft-deleted (global filter).</summary>
    public static async Task EnsureCustomerAsync(IApplicationDbContext db, Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, ct))
            throw new NotFoundException(nameof(Customer), customerId);
    }

    /// <summary>Maps documents, resolving every uploader/updater/deleter name in ONE query (no N+1).</summary>
    public static async Task<List<CustomerDocumentDto>> ToDtosAsync(IApplicationDbContext db, IReadOnlyCollection<CustomerDocument> docs, CancellationToken ct)
    {
        var actorIds = docs
            .SelectMany(d => new[] { d.CreatedById, d.UpdatedById, d.DeletedById })
            .Where(id => id.HasValue).Select(id => id!.Value)
            .Distinct().ToList();

        // Same display rule as the record-audit footer: username, falling back to email. Include deleted staff.
        var names = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, u.Email })
                .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.Username) ? u.Email : u.Username, ct);

        string? Name(Guid? id) => id is Guid g && names.TryGetValue(g, out var n) ? n : null;

        return docs.Select(d => new CustomerDocumentDto
        {
            Id = d.Id,
            DocumentType = d.DocumentType,
            DocumentTypeLabel = CustomerDocumentTypes.Label(d.DocumentType),
            OriginalFileName = d.OriginalFileName,
            MimeType = d.MimeType,
            FileSizeBytes = d.FileSizeBytes,
            Note = d.Note,
            IsInline = CustomerDocumentFiles.IsInline(d.MimeType),
            Status = d.Deleted ? CustomerDocumentStatus.Deleted : CustomerDocumentStatus.Active,
            UploadedOnUtc = d.CreatedOnUtc,
            UploadedBy = Name(d.CreatedById),
            UpdatedOnUtc = d.UpdatedOnUtc,
            UpdatedBy = Name(d.UpdatedById),
            DeletedOnUtc = d.DeletedOnUtc,
            DeletedBy = Name(d.DeletedById),
        }).ToList();
    }
}
