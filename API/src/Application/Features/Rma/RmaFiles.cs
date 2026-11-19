using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>
/// Photos / documents on a return (RT-27, RT-97): stored in the private file store (never web-reachable),
/// validated by content (magic bytes) rather than the declared type, random storage names, size-capped.
/// </summary>
public static class RmaFiles
{
    public const int MaxFilesPerRequest = 5;
    public const int MaxFilesPerRma = 20;
    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>Validates and stores <paramref name="upload"/>, adding the attachment row.</summary>
    public static async Task<RmaAttachment> SaveAsync(
        IApplicationDbContext db, IPrivateFileStore store, Domain.Entities.Rma rma, Guid? lineId, RmaUpload upload,
        RmaActorType byType, Guid? byId, DateTime now, CancellationToken ct)
    {
        if (upload.Length <= 0 || upload.Length > MaxBytes)
            throw Invalid($"'{upload.FileName}' must be between 1 byte and 10 MB.");

        await using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxBytes)
            throw Invalid($"'{upload.FileName}' is larger than 10 MB.");

        var kind = Sniff(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)))
            ?? throw Invalid($"'{upload.FileName}' is not a JPG, PNG, WEBP, HEIC or PDF file.");

        buffer.Position = 0;
        var key = await store.SaveAsync($"rma/{rma.Id:N}", kind.Extension, buffer, kind.Mime, ct);

        var attachment = new RmaAttachment
        {
            RmaId = rma.Id,
            RmaLineItemId = lineId,
            StorageKey = key,
            OriginalFileName = SafeName(upload.FileName, kind.Extension),
            MimeType = kind.Mime,
            SizeBytes = buffer.Length,
            UploadedByType = byType,
            UploadedById = byId,
            CreatedOnUtc = now,
        };
        db.RmaAttachments.Add(attachment);
        return attachment;
    }

    private record FileKind(string Extension, string Mime);

    private static FileKind? Sniff(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return new(".jpg", "image/jpeg");
        if (h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return new(".png", "image/png");
        if (h.Length >= 12 && h[0] == (byte)'R' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'F'
            && h[8] == (byte)'W' && h[9] == (byte)'E' && h[10] == (byte)'B' && h[11] == (byte)'P') return new(".webp", "image/webp");
        if (h.Length >= 12 && h[4] == (byte)'f' && h[5] == (byte)'t' && h[6] == (byte)'y' && h[7] == (byte)'p')
        {
            var brand = System.Text.Encoding.ASCII.GetString(h.Slice(8, 4));
            if (brand is "heic" or "heix" or "mif1" or "msf1" or "hevc") return new(".heic", "image/heic");
        }
        if (h.Length >= 4 && h[0] == (byte)'%' && h[1] == (byte)'P' && h[2] == (byte)'D' && h[3] == (byte)'F') return new(".pdf", "application/pdf");
        return null;
    }

    private static string SafeName(string name, string ext)
    {
        var baseName = Path.GetFileNameWithoutExtension(name ?? "file");
        var clean = new string(baseName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim();
        if (clean.Length == 0) clean = "file";
        if (clean.Length > 100) clean = clean[..100];
        return clean + ext;
    }

    private static ValidationException Invalid(string message)
        => new(new[] { new ValidationFailure("Photos", message) });
}

/// <summary>Adds a file to a return — staff any time, the customer while the return is open (own returns only).</summary>
public record AddRmaAttachmentCommand(Guid RmaId, bool AsCustomer, RmaUpload File, Guid? RmaLineItemId = null) : IRequest<RmaAttachmentDto>;

public class AddRmaAttachmentCommandHandler : IRequestHandler<AddRmaAttachmentCommand, RmaAttachmentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPrivateFileStore _store;
    private readonly IDateTimeProvider _clock;

    public AddRmaAttachmentCommandHandler(IApplicationDbContext db, ICurrentUserService current, IPrivateFileStore store, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _store = store;
        _clock = clock;
    }

    public async Task<RmaAttachmentDto> Handle(AddRmaAttachmentCommand request, CancellationToken cancellationToken)
    {
        var rma = await _db.Rmas.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == request.RmaId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Rma), request.RmaId);

        Guid? byId = _current.UserId;
        var byType = RmaActorType.Admin;
        if (request.AsCustomer)
        {
            var customer = await RmaSupport.CurrentCustomerAsync(_db, _current, cancellationToken);
            if (rma.CustomerId != customer.Id)
                throw new NotFoundException(nameof(Domain.Entities.Rma), request.RmaId);
            if (!RmaWorkflow.IsOpen(rma.Status) || rma.Status is RmaStatus.Refunded or RmaStatus.Exchanged)
                throw new ConflictException("Files can no longer be added to this return.");
            byType = RmaActorType.Customer;
            byId = customer.Id;
        }

        if (request.RmaLineItemId is Guid lid && rma.Lines.All(l => l.Id != lid))
            throw new NotFoundException(nameof(RmaLineItem), lid);

        var count = await _db.RmaAttachments.CountAsync(a => a.RmaId == rma.Id, cancellationToken);
        if (count >= RmaFiles.MaxFilesPerRma)
            throw new ConflictException($"A return can hold at most {RmaFiles.MaxFilesPerRma} files.");

        var attachment = await RmaFiles.SaveAsync(_db, _store, rma, request.RmaLineItemId, request.File, byType, byId, _clock.UtcNow, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return RmaAttachmentDto.From(attachment);
    }
}

/// <summary>An opened attachment to stream back.</summary>
public record RmaFileResult(Stream Content, string ContentType, string FileName);

/// <summary>Opens an attachment — staff any, customer only their own return's (RT-27 / RT-95).</summary>
public record GetRmaAttachmentQuery(Guid RmaId, Guid AttachmentId, bool AsCustomer) : IRequest<RmaFileResult>;

public class GetRmaAttachmentQueryHandler : IRequestHandler<GetRmaAttachmentQuery, RmaFileResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPrivateFileStore _store;

    public GetRmaAttachmentQueryHandler(IApplicationDbContext db, ICurrentUserService current, IPrivateFileStore store)
    {
        _db = db;
        _current = current;
        _store = store;
    }

    public async Task<RmaFileResult> Handle(GetRmaAttachmentQuery request, CancellationToken cancellationToken)
    {
        var row = await _db.RmaAttachments.AsNoTracking()
            .Where(a => a.Id == request.AttachmentId && a.RmaId == request.RmaId)
            .Select(a => new { a.StorageKey, a.MimeType, a.OriginalFileName, a.Rma!.CustomerId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(RmaAttachment), request.AttachmentId);

        if (request.AsCustomer)
        {
            var customer = await RmaSupport.CurrentCustomerAsync(_db, _current, cancellationToken);
            if (row.CustomerId != customer.Id)
                throw new NotFoundException(nameof(RmaAttachment), request.AttachmentId);
        }

        var stream = await _store.OpenReadAsync(row.StorageKey, cancellationToken);
        return new RmaFileResult(stream, row.MimeType, row.OriginalFileName);
    }
}
