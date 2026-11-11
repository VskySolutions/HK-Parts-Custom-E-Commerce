using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerDocuments;

/// <summary>One row of the "Upload Documents" dialog.</summary>
public sealed record UploadCustomerDocumentItem(CustomerDocumentType? DocumentType, string? Note, DocumentFileInput? File);

/// <summary>
/// Bulk upload (all or nothing): every row is validated first (per-row errors keyed <c>Items[i].Field</c>), then
/// all files are written to private storage and all rows saved in ONE <c>SaveChanges</c>. If saving fails, the files
/// already written are removed again, so nothing partial is left behind. Returns the refreshed tab payload.
/// </summary>
public record UploadCustomerDocumentsCommand(Guid CustomerId, IReadOnlyList<UploadCustomerDocumentItem> Items)
    : IRequest<CustomerDocumentListDto>;

public class UploadCustomerDocumentsCommandValidator : AbstractValidator<UploadCustomerDocumentsCommand>
{
    public UploadCustomerDocumentsCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Add at least one document.")
            .Must(i => i is null || i.Count <= CustomerDocumentFiles.MaxFilesPerUpload)
            .WithMessage($"Upload at most {CustomerDocumentFiles.MaxFilesPerUpload} documents at a time.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DocumentType)
                .NotNull().WithMessage("Document Name is required.")
                .IsInEnum().WithMessage("Select a valid Document Name.");
            item.RuleFor(i => i.Note).MaximumLength(2000);
            item.RuleFor(i => i.File).Custom((file, ctx) =>
            {
                var error = CustomerDocumentFiles.Validate(file, out _, out _);
                if (error is not null)
                    ctx.AddFailure(error);
            });
        });
    }
}

public class UploadCustomerDocumentsCommandHandler : IRequestHandler<UploadCustomerDocumentsCommand, CustomerDocumentListDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPrivateFileStore _files;
    private readonly IMediator _mediator;

    public UploadCustomerDocumentsCommandHandler(IApplicationDbContext db, IPrivateFileStore files, IMediator mediator)
    {
        _db = db;
        _files = files;
        _mediator = mediator;
    }

    public async Task<CustomerDocumentListDto> Handle(UploadCustomerDocumentsCommand request, CancellationToken cancellationToken)
    {
        await CustomerDocumentMapper.EnsureCustomerAsync(_db, request.CustomerId, cancellationToken);

        var written = new List<string>();
        try
        {
            foreach (var item in request.Items)
            {
                // Already validated by the pipeline; re-derive the content-detected type for storage.
                CustomerDocumentFiles.Validate(item.File, out var ext, out var mime);
                var file = item.File!;

                string key;
                await using (var stream = new MemoryStream(file.Content, writable: false))
                    key = await _files.SaveAsync($"customer-documents/{request.CustomerId:N}", ext, stream, mime, cancellationToken);
                written.Add(key);

                _db.CustomerDocuments.Add(new CustomerDocument
                {
                    CustomerId = request.CustomerId,
                    DocumentType = item.DocumentType!.Value,
                    OriginalFileName = CustomerDocumentFiles.SanitizeFileName(file.FileName),
                    StorageKey = key,
                    MimeType = mime,
                    FileSizeBytes = file.Content.LongLength,
                    Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim(),
                });
            }

            // CreatedById (uploader) / UpdatedById are stamped by the DbContext; one save = one transaction.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var key in written)
                await _files.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return await _mediator.Send(new ListCustomerDocumentsQuery(request.CustomerId), cancellationToken);
    }
}
