using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.CustomerDocuments;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Customer → Documents tab: list, bulk upload, view/download, edit and soft-delete of admin-kept customer documents.
/// Same access model as <see cref="AdminCustomersController"/>: reads need the Customers module, writes also
/// <c>Users.Write</c>. Files live in private storage and are only ever streamed through <see cref="GetFile"/>.
/// </summary>
[Route("api/admin/customers/{customerId:guid}/documents")]
[RequireModule(Modules.Customers)]
public class AdminCustomerDocumentsController : ApiControllerBase
{
    /// <summary>10 files × 10 MB plus multipart/form overhead.</summary>
    private const long UploadLimitBytes = CustomerDocumentFiles.MaxFilesPerUpload * CustomerDocumentFiles.MaxSizeBytes + 1_048_576;

    /// <summary>The document-type dropdown options.</summary>
    [HttpGet("~/api/admin/customers/document-types")]
    public async Task<ActionResult<IReadOnlyList<CustomerDocumentTypeOption>>> Types()
        => Ok(await Mediator.Send(new ListCustomerDocumentTypesQuery()));

    /// <summary>All documents (active + deleted, newest first) and the active count for the tab badge.</summary>
    [HttpGet]
    public async Task<ActionResult<CustomerDocumentListDto>> List(Guid customerId)
        => Ok(await Mediator.Send(new ListCustomerDocumentsQuery(customerId)));

    /// <summary>
    /// Bulk upload (multipart): <c>items[i].documentType</c>, <c>items[i].note</c>, <c>items[i].file</c>. All or nothing;
    /// per-row validation errors are returned as <c>Items[i].Field</c>. Returns the refreshed list.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.UsersWrite)]
    [RequestSizeLimit(UploadLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimitBytes)]
    public async Task<ActionResult<CustomerDocumentListDto>> Upload(
        Guid customerId, [FromForm] List<DocumentUploadForm>? items, CancellationToken cancellationToken)
    {
        var rows = new List<UploadCustomerDocumentItem>();
        foreach (var item in items ?? new List<DocumentUploadForm>())
            rows.Add(new UploadCustomerDocumentItem(item.DocumentType, item.Note, await ReadAsync(item.File, cancellationToken)));

        return Ok(await Mediator.Send(new UploadCustomerDocumentsCommand(customerId, rows), cancellationToken));
    }

    /// <summary>Open (PDF/images, inline) or download (DOC/DOCX) a document's file. Never redirects to storage.</summary>
    [HttpGet("{documentId:guid}/file")]
    public async Task<IActionResult> GetFile(Guid customerId, Guid documentId, CancellationToken cancellationToken)
    {
        var file = await Mediator.Send(new GetCustomerDocumentFileQuery(customerId, documentId), cancellationToken);

        Response.Headers[HeaderNames.CacheControl] = "no-store";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        var disposition = new ContentDispositionHeaderValue(file.Inline ? "inline" : "attachment");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        return File(file.Content, file.MimeType);
    }

    /// <summary>Edit an active document (multipart): <c>documentType</c>, <c>note</c>, optional replacement <c>file</c>.</summary>
    [HttpPut("{documentId:guid}")]
    [RequirePermission(Permissions.UsersWrite)]
    [RequestSizeLimit(CustomerDocumentFiles.MaxSizeBytes + 1_048_576)]
    [RequestFormLimits(MultipartBodyLengthLimit = CustomerDocumentFiles.MaxSizeBytes + 1_048_576)]
    public async Task<ActionResult<CustomerDocumentDto>> Update(
        Guid customerId, Guid documentId, [FromForm] DocumentUploadForm form, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new UpdateCustomerDocumentCommand(
            customerId, documentId, form.DocumentType, form.Note, await ReadAsync(form.File, cancellationToken)), cancellationToken));

    /// <summary>Soft-delete a document (record + file kept for audit). 409 if it is already deleted.</summary>
    [HttpDelete("{documentId:guid}")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<IActionResult> Delete(Guid customerId, Guid documentId)
    {
        await Mediator.Send(new DeleteCustomerDocumentCommand(customerId, documentId));
        return NoContent();
    }

    /// <summary>Buffers an uploaded file (bounded by the request size limits above), like the other upload endpoints.</summary>
    private static async Task<DocumentFileInput?> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return null;
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return new DocumentFileInput(ms.ToArray(), file.FileName);
    }
}

/// <summary>Multipart shape of one document row (upload) or of the edit form.</summary>
public class DocumentUploadForm
{
    public CustomerDocumentType? DocumentType { get; set; }
    public string? Note { get; set; }
    public IFormFile? File { get; set; }
}
