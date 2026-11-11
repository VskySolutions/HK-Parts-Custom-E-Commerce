using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.CustomerDocuments;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

internal static class DocFiles
{
    public static byte[] Pdf => Encoding.ASCII.GetBytes("%PDF-1.7\n%test\n");
    public static byte[] Png => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
    public static byte[] Jpg => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 };
    public static byte[] Doc => new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0 };

    public static byte[] Zip(params string[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var e in entries)
                using (var w = new StreamWriter(zip.CreateEntry(e).Open())) w.Write("<x/>");
        return ms.ToArray();
    }

    public static byte[] Docx => Zip("[Content_Types].xml", "word/document.xml");
}

/// <summary>File rules: content-based type detection, extension agreement, size, name sanitizing (no database).</summary>
public class CustomerDocumentFileRuleTests
{
    [Theory]
    [InlineData("a.pdf", "application/pdf")]
    [InlineData("a.PNG", "image/png")]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.doc", "application/msword")]
    [InlineData("a.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void Accepts_allowed_types_by_content(string name, string mime)
    {
        var bytes = mime switch
        {
            "application/pdf" => DocFiles.Pdf, "image/png" => DocFiles.Png, "image/jpeg" => DocFiles.Jpg,
            "application/msword" => DocFiles.Doc, _ => DocFiles.Docx,
        };
        Assert.Null(CustomerDocumentFiles.Validate(new DocumentFileInput(bytes, name), out _, out var detected));
        Assert.Equal(mime, detected);
    }

    [Fact]
    public void Rejects_renamed_executable_and_mismatched_extension()
    {
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // "MZ"
        Assert.NotNull(CustomerDocumentFiles.Validate(new DocumentFileInput(exe, "license.pdf"), out _, out _));
        Assert.NotNull(CustomerDocumentFiles.Validate(new DocumentFileInput(DocFiles.Png, "license.pdf"), out _, out _));
        Assert.NotNull(CustomerDocumentFiles.Validate(new DocumentFileInput(DocFiles.Zip("a.txt"), "x.docx"), out _, out _)); // zip, not Word
    }

    [Theory]
    [InlineData("a.exe")]
    [InlineData("a.gif")]
    [InlineData("a")]
    public void Rejects_disallowed_extensions(string name)
        => Assert.Equal(CustomerDocumentFiles.AllowedText, CustomerDocumentFiles.Validate(new DocumentFileInput(DocFiles.Pdf, name), out _, out _));

    [Fact]
    public void Rejects_empty_and_oversized_files()
    {
        Assert.Equal("File is required.", CustomerDocumentFiles.Validate(new DocumentFileInput(Array.Empty<byte>(), "a.pdf"), out _, out _));
        var big = new byte[CustomerDocumentFiles.MaxSizeBytes + 1];
        DocFiles.Pdf.CopyTo(big, 0);
        Assert.Contains("10 MB", CustomerDocumentFiles.Validate(new DocumentFileInput(big, "a.pdf"), out _, out _));
    }

    [Theory]
    [InlineData(@"C:\fakepath\..\..\secret.pdf", "secret.pdf")]
    [InlineData("../../etc/pass<wd>.pdf", "pass_wd_.pdf")]
    [InlineData("  my   license \u0007.pdf ", "my license _.pdf")]
    [InlineData(".pdf", "document.pdf")]
    public void Sanitizes_file_names(string input, string expected)
        => Assert.Equal(expected, CustomerDocumentFiles.SanitizeFileName(input));

    [Fact]
    public void Upload_validator_reports_errors_per_row()
    {
        var cmd = new UploadCustomerDocumentsCommand(Guid.NewGuid(), new[]
        {
            new UploadCustomerDocumentItem(CustomerDocumentType.MilitaryId, null, new DocumentFileInput(DocFiles.Pdf, "ok.pdf")),
            new UploadCustomerDocumentItem(null, null, new DocumentFileInput(DocFiles.Pdf, "bad.exe")),
        });
        var errors = new UploadCustomerDocumentsCommandValidator().Validate(cmd).Errors;

        Assert.DoesNotContain(errors, e => e.PropertyName.StartsWith("Items[0]"));
        Assert.Contains(errors, e => e.PropertyName == "Items[1].DocumentType");
        Assert.Contains(errors, e => e.PropertyName == "Items[1].File");
    }

    [Fact]
    public void Upload_validator_rejects_empty_and_too_many_rows()
    {
        var v = new UploadCustomerDocumentsCommandValidator();
        Assert.Contains(v.Validate(new UploadCustomerDocumentsCommand(Guid.NewGuid(), Array.Empty<UploadCustomerDocumentItem>())).Errors,
            e => e.PropertyName == "Items");
        var eleven = Enumerable.Range(0, 11)
            .Select(_ => new UploadCustomerDocumentItem(CustomerDocumentType.MilitaryId, null, new DocumentFileInput(DocFiles.Pdf, "a.pdf")))
            .ToList();
        Assert.Contains(v.Validate(new UploadCustomerDocumentsCommand(Guid.NewGuid(), eleven)).Errors, e => e.PropertyName == "Items");
    }

    [Fact]
    public void Document_types_come_from_the_backend_list_in_dropdown_order()
        => Assert.Equal(
            new[]
            {
                "Government-Issued Photo ID", "Drivers License", "Law Enforcement ID", "Military ID",
                "Firearm License / Permit", "Firearm Training Certificate", "License Copy", "Address Proof",
                "Additional Supporting Documents",
            },
            CustomerDocumentTypes.All.Select(t => t.Label));

    [Fact]
    public void Every_document_type_has_a_label_and_existing_values_are_unchanged()
    {
        Assert.Equal(Enum.GetValues<CustomerDocumentType>().OrderBy(v => v), CustomerDocumentTypes.All.Select(t => t.Value).OrderBy(v => v));
        // Saved documents reference these ints — they must never be renumbered.
        Assert.Equal(1, (int)CustomerDocumentType.FirearmLicensePermit);
        Assert.Equal(2, (int)CustomerDocumentType.MilitaryId);
        Assert.Equal(3, (int)CustomerDocumentType.LawEnforcementId);
        Assert.Equal(4, (int)CustomerDocumentType.DriversLicense);
    }
}

/// <summary>Upload / list / edit / soft-delete against the real SQL Server test DB with an in-memory private store.</summary>
public class CustomerDocumentHandlerTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryPrivateStore _store = new();

    private Guid SeedCustomer()
    {
        using var db = NewContext();
        var user = new User
        {
            Username = $"u{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@test.local", PasswordHash = "x",
            Customer = new Customer { FirstName = "Doc", LastName = "Owner" },
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Customer!.Id;
    }

    private UploadCustomerDocumentsCommandHandler Upload(AppDbContext db) => new(db, _store, new ListOnlyMediator(db));

    private static UploadCustomerDocumentItem Row(CustomerDocumentType t, byte[] bytes, string name, string? note = null)
        => new(t, note, new DocumentFileInput(bytes, name));

    [Fact]
    public async Task Upload_saves_all_rows_privately_and_lists_newest_first_with_active_count()
    {
        var customerId = SeedCustomer();
        using var db = NewContext();
        var result = await Upload(db).Handle(new UploadCustomerDocumentsCommand(customerId, new[]
        {
            Row(CustomerDocumentType.DriversLicense, DocFiles.Pdf, "dl.pdf", "front"),
            Row(CustomerDocumentType.MilitaryId, DocFiles.Png, "mil.png"),
        }), CancellationToken.None);

        Assert.Equal(2, result.ActiveCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal(CustomerDocumentStatus.Active, i.Status));
        Assert.Equal(2, _store.Files.Count);
        Assert.All(_store.Files.Keys, k => Assert.StartsWith($"customer-documents/{customerId:N}/", k));
        var row = await db.CustomerDocuments.AsNoTracking().FirstAsync(d => d.OriginalFileName == "dl.pdf");
        Assert.Equal("application/pdf", row.MimeType);
        Assert.Equal("front", row.Note);
    }

    [Fact]
    public async Task Upload_is_all_or_nothing_when_saving_fails()
    {
        var customerId = SeedCustomer();
        _store.FailAfter = 1; // second file write throws
        using var db = NewContext();

        await Assert.ThrowsAsync<IOException>(() => Upload(db).Handle(new UploadCustomerDocumentsCommand(customerId, new[]
        {
            Row(CustomerDocumentType.DriversLicense, DocFiles.Pdf, "a.pdf"),
            Row(CustomerDocumentType.MilitaryId, DocFiles.Pdf, "b.pdf"),
        }), CancellationToken.None));

        Assert.Empty(_store.Files); // the first file was removed again
        using var check = NewContext();
        Assert.False(await check.CustomerDocuments.AnyAsync(d => d.CustomerId == customerId));
    }

    [Fact]
    public async Task Upload_for_unknown_customer_throws_not_found()
    {
        using var db = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => Upload(db).Handle(
            new UploadCustomerDocumentsCommand(Guid.NewGuid(), new[] { Row(CustomerDocumentType.MilitaryId, DocFiles.Pdf, "a.pdf") }),
            CancellationToken.None));
    }

    [Fact]
    public async Task Edit_changes_type_note_and_replaces_file_removing_the_old_one()
    {
        var customerId = SeedCustomer();
        Guid docId;
        string oldKey;
        using (var db = NewContext())
        {
            var list = await Upload(db).Handle(new UploadCustomerDocumentsCommand(customerId,
                new[] { Row(CustomerDocumentType.DriversLicense, DocFiles.Pdf, "old.pdf") }), CancellationToken.None);
            docId = list.Items[0].Id;
            oldKey = _store.Files.Keys.Single();
        }

        using (var db = NewContext())
        {
            var dto = await new UpdateCustomerDocumentCommandHandler(db, _store).Handle(new UpdateCustomerDocumentCommand(
                customerId, docId, CustomerDocumentType.LawEnforcementId, "renewed", new DocumentFileInput(DocFiles.Jpg, "new.jpg")),
                CancellationToken.None);
            Assert.Equal(CustomerDocumentType.LawEnforcementId, dto.DocumentType);
            Assert.Equal("renewed", dto.Note);
            Assert.Equal("new.jpg", dto.OriginalFileName);
            Assert.Equal("image/jpeg", dto.MimeType);
        }

        Assert.DoesNotContain(oldKey, _store.Files.Keys);
        Assert.Single(_store.Files);
    }

    [Fact]
    public async Task Delete_is_soft_keeps_file_and_blocks_edit_and_second_delete()
    {
        var customerId = SeedCustomer();
        Guid docId;
        using (var db = NewContext())
            docId = (await Upload(db).Handle(new UploadCustomerDocumentsCommand(customerId,
                new[] { Row(CustomerDocumentType.MilitaryId, DocFiles.Pdf, "a.pdf") }), CancellationToken.None)).Items[0].Id;

        var adminId = Guid.NewGuid();
        using (var db = NewContext())
            await new DeleteCustomerDocumentCommandHandler(db, new FixedUser(adminId), new FixedClock(Now))
                .Handle(new DeleteCustomerDocumentCommand(customerId, docId), CancellationToken.None);

        using (var db = NewContext())
        {
            var row = await db.CustomerDocuments.AsNoTracking().SingleAsync(d => d.Id == docId);
            Assert.True(row.Deleted);
            Assert.Equal(Now, row.DeletedOnUtc);
            Assert.Equal(adminId, row.DeletedById);
            Assert.Single(_store.Files); // file kept for audit

            var list = await new ListCustomerDocumentsQueryHandler(db).Handle(new ListCustomerDocumentsQuery(customerId), CancellationToken.None);
            Assert.Equal(0, list.ActiveCount);
            Assert.Equal(CustomerDocumentStatus.Deleted, Assert.Single(list.Items).Status); // still listed
        }

        using (var db = NewContext())
        {
            await Assert.ThrowsAsync<ConflictException>(() => new DeleteCustomerDocumentCommandHandler(db, new FixedUser(adminId), new FixedClock(Now))
                .Handle(new DeleteCustomerDocumentCommand(customerId, docId), CancellationToken.None));
            await Assert.ThrowsAsync<ConflictException>(() => new UpdateCustomerDocumentCommandHandler(db, _store)
                .Handle(new UpdateCustomerDocumentCommand(customerId, docId, CustomerDocumentType.MilitaryId, null, null), CancellationToken.None));
        }
    }

    private sealed class InMemoryPrivateStore : IPrivateFileStore
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public int? FailAfter { get; set; }

        public async Task<string> SaveAsync(string folder, string extension, Stream content, string contentType, CancellationToken ct = default)
        {
            if (FailAfter is int n && Files.Count >= n)
                throw new IOException("simulated storage failure");
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            var key = $"{folder}/{Guid.NewGuid():N}{extension}";
            Files[key] = ms.ToArray();
            return key;
        }

        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
            => Files.TryGetValue(key, out var b) ? Task.FromResult<Stream>(new MemoryStream(b)) : throw new NotFoundException("missing");

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            Files.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedUser : ICurrentUserService
    {
        public FixedUser(Guid id) => UserId = id;
        public Guid? UserId { get; }
        public string? Email => null;
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => Array.Empty<string>();
        public bool IsInRole(string role) => false;
        public string? CorrelationId => null;
        public string? IpAddress => null;
    }

    /// <summary>Routes the upload handler's follow-up list query to the real handler.</summary>
    private sealed class ListOnlyMediator : MediatR.IMediator
    {
        private readonly IApplicationDbContext _db;
        public ListOnlyMediator(IApplicationDbContext db) => _db = db;

        public async Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken ct = default)
            => request is ListCustomerDocumentsQuery q
                ? (TResponse)(object)await new ListCustomerDocumentsQueryHandler(_db).Handle(q, ct)
                : throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : MediatR.IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : MediatR.INotification => Task.CompletedTask;
    }
}
