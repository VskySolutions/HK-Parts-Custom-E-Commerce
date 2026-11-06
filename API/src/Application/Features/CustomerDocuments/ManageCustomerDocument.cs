using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerDocuments;

// ---------------------------------------------------------------- Edit

/// <summary>
/// Edit an active document: type, note and optionally a replacement file. The uploader (CreatedById) never changes;
/// UpdatedById/UpdatedOnUtc are stamped by the DbContext. A replaced file follows the project convention
/// (commit first, then best-effort delete of the old file); if the save fails, the new file is removed instead.
/// </summary>
public record UpdateCustomerDocumentCommand(
    Guid CustomerId, Guid DocumentId, CustomerDocumentType? DocumentType, string? Note, DocumentFileInput? File)
    : IRequest<CustomerDocumentDto>;

public class UpdateCustomerDocumentCommandValidator : AbstractValidator<UpdateCustomerDocumentCommand>
{
    public UpdateCustomerDocumentCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.DocumentType)
            .NotNull().WithMessage("Document Name is required.")
            .IsInEnum().WithMessage("Select a valid Document Name.");
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.File).Custom((file, ctx) =>
        {
            if (file is null) return; // replacing the file is optional
            var error = CustomerDocumentFiles.Validate(file, out _, out _);
            if (error is not null)
                ctx.AddFailure(error);
        });
    }
}

public class UpdateCustomerDocumentCommandHandler : IRequestHandler<UpdateCustomerDocumentCommand, CustomerDocumentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPrivateFileStore _files;

    public UpdateCustomerDocumentCommandHandler(IApplicationDbContext db, IPrivateFileStore files)
    {
        _db = db;
        _files = files;
    }

    public async Task<CustomerDocumentDto> Handle(UpdateCustomerDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = await CustomerDocumentLookup.FindActiveAsync(_db, request.CustomerId, request.DocumentId, "edited", cancellationToken);

        doc.DocumentType = request.DocumentType!.Value;
        doc.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        string? newKey = null;
        var oldKey = doc.StorageKey;
        if (request.File is not null)
        {
            CustomerDocumentFiles.Validate(request.File, out var ext, out var mime);
            await using (var stream = new MemoryStream(request.File.Content, writable: false))
                newKey = await _files.SaveAsync($"customer-documents/{request.CustomerId:N}", ext, stream, mime, cancellationToken);

            doc.StorageKey = newKey;
            doc.OriginalFileName = CustomerDocumentFiles.SanitizeFileName(request.File.FileName);
            doc.MimeType = mime;
            doc.FileSizeBytes = request.File.Content.LongLength;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (newKey is not null)
                await _files.DeleteAsync(newKey, CancellationToken.None);
            throw;
        }

        if (newKey is not null)
            await _files.DeleteAsync(oldKey, CancellationToken.None); // best-effort, after commit

        return (await CustomerDocumentMapper.ToDtosAsync(_db, new[] { doc }, cancellationToken))[0];
    }
}

// ---------------------------------------------------------------- Delete

/// <summary>
/// Soft-delete: marks the document deleted with who/when, keeping the row and the stored file for audit.
/// Deleted documents cannot be edited, restored or deleted again (409). The audit trail records it as a Delete.
/// </summary>
public record DeleteCustomerDocumentCommand(Guid CustomerId, Guid DocumentId) : IRequest;

public class DeleteCustomerDocumentCommandHandler : IRequestHandler<DeleteCustomerDocumentCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;

    public DeleteCustomerDocumentCommandHandler(IApplicationDbContext db, ICurrentUserService current, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _clock = clock;
    }

    public async Task Handle(DeleteCustomerDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = await CustomerDocumentLookup.FindActiveAsync(_db, request.CustomerId, request.DocumentId, "deleted again", cancellationToken);

        doc.Deleted = true;
        doc.DeletedOnUtc = _clock.UtcNow;
        doc.DeletedById = _current.UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

// ---------------------------------------------------------------- View / download

/// <summary>An opened stored file plus how to serve it. The caller owns (and must dispose) the stream.</summary>
public sealed record CustomerDocumentFile(Stream Content, string MimeType, string FileName, bool Inline);

/// <summary>Opens a document's file for the permission-checked view/download endpoint (active or deleted, for audit).</summary>
public record GetCustomerDocumentFileQuery(Guid CustomerId, Guid DocumentId) : IRequest<CustomerDocumentFile>;

public class GetCustomerDocumentFileQueryHandler : IRequestHandler<GetCustomerDocumentFileQuery, CustomerDocumentFile>
{
    private readonly IApplicationDbContext _db;
    private readonly IPrivateFileStore _files;

    public GetCustomerDocumentFileQueryHandler(IApplicationDbContext db, IPrivateFileStore files)
    {
        _db = db;
        _files = files;
    }

    public async Task<CustomerDocumentFile> Handle(GetCustomerDocumentFileQuery request, CancellationToken cancellationToken)
    {
        await CustomerDocumentMapper.EnsureCustomerAsync(_db, request.CustomerId, cancellationToken);
        var doc = await _db.CustomerDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.CustomerId == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerDocument), request.DocumentId);

        var stream = await _files.OpenReadAsync(doc.StorageKey, cancellationToken);
        return new CustomerDocumentFile(stream, doc.MimeType, doc.OriginalFileName, CustomerDocumentFiles.IsInline(doc.MimeType));
    }
}

internal static class CustomerDocumentLookup
{
    /// <summary>Loads a tracked document of this (non-deleted) customer; 404 if missing, 409 if already deleted.</summary>
    public static async Task<CustomerDocument> FindActiveAsync(
        IApplicationDbContext db, Guid customerId, Guid documentId, string verb, CancellationToken ct)
    {
        await CustomerDocumentMapper.EnsureCustomerAsync(db, customerId, ct);
        var doc = await db.CustomerDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.CustomerId == customerId, ct)
            ?? throw new NotFoundException(nameof(CustomerDocument), documentId);
        if (doc.Deleted)
            throw new ConflictException($"This document has been deleted and cannot be {verb}.");
        return doc;
    }
}
