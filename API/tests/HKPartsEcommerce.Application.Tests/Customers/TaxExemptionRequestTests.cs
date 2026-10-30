using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.TaxExemptionRequests;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>
/// The customer tax-exemption workflow (REQ-TAX-003): submit → pending, admin approve marks the customer
/// exempt and copies the evidence, reject leaves them taxable and free to resubmit. Backed by the real
/// SQL Server test DB.
/// </summary>
public class TaxExemptionRequestTests : CatalogTestBase
{
    private (Guid CustomerId, Guid UserId) SeedCustomer()
    {
        using var db = NewContext();
        var user = new User
        {
            Username = $"u{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@test.local",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        var customer = new Customer { UserId = user.Id, FirstName = "Test", LastName = "Buyer" };
        db.Customers.Add(customer);
        db.SaveChanges();
        return (customer.Id, user.Id);
    }

    /// <summary>
    /// Seeds a media row owned by the given user. CreatedById is stamped by the DbContext from the current
    /// user (not from an assignment), so the seed context must carry that user — exactly as the real customer
    /// upload does — for the submit handler's ownership check to see it.
    /// </summary>
    private Guid SeedOwnedMedia(Guid userId)
    {
        using var db = new AppDbContext(_options, new FakeCurrentUser { UserId = userId });
        db.Database.UseTransaction(_transaction);
        var media = new Media
        {
            OriginalFileName = "cert.pdf",
            SeoFileName = $"cert-{Guid.NewGuid():N}",
            AssetKey = Guid.NewGuid().ToString("N"),
            Url = "https://cdn/cert.pdf",
            MediaType = MediaType.Document,
            MimeType = "application/pdf",
            FileSizeBytes = 1024,
        };
        db.Media.Add(media);
        db.SaveChanges();
        return media.Id;
    }

    private SubmitTaxExemptionRequestCommandHandler SubmitHandler(Guid userId)
        => new(NewContext(), new FakeCurrentUser { UserId = userId }, new FixedClock(DateTime.UtcNow), new FakeEmailTemplateSender());

    private ResolveTaxExemptionRequestCommandHandler ResolveHandler(Guid adminUserId)
        => new(NewContext(), new FakeCurrentUser { UserId = adminUserId }, new FixedClock(DateTime.UtcNow), new FakeEmailTemplateSender());

    [Fact]
    public async Task Submit_creates_pending_request_with_documents()
    {
        var (_, userId) = SeedCustomer();
        var mediaId = SeedOwnedMedia(userId);

        var dto = await SubmitHandler(userId).Handle(
            new SubmitTaxExemptionRequestCommand("CERT-1", null, new List<Guid> { mediaId }), CancellationToken.None);

        Assert.Equal(TaxExemptionRequestStatus.PendingReview, dto.Status);
        Assert.Single(dto.Documents);
        Assert.Equal(mediaId, dto.Documents[0].MediaId);
    }

    [Fact]
    public void Submit_validator_requires_certificate_or_vat()
    {
        var result = new SubmitTaxExemptionRequestCommandValidator().Validate(
            new SubmitTaxExemptionRequestCommand(null, null, new List<Guid> { Guid.NewGuid() }));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Submit_validator_requires_a_document()
    {
        var result = new SubmitTaxExemptionRequestCommandValidator().Validate(
            new SubmitTaxExemptionRequestCommand("CERT-1", null, new List<Guid>()));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Submit_rejects_media_owned_by_another_user()
    {
        var (_, userId) = SeedCustomer();
        var (_, otherUserId) = SeedCustomer();
        var foreignMedia = SeedOwnedMedia(otherUserId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SubmitHandler(userId).Handle(
                new SubmitTaxExemptionRequestCommand("CERT-1", null, new List<Guid> { foreignMedia }),
                CancellationToken.None));
    }

    [Fact]
    public async Task Submit_twice_while_pending_conflicts()
    {
        var (_, userId) = SeedCustomer();
        var mediaId = SeedOwnedMedia(userId);

        await SubmitHandler(userId).Handle(
            new SubmitTaxExemptionRequestCommand("CERT-1", null, new List<Guid> { mediaId }), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            SubmitHandler(userId).Handle(
                new SubmitTaxExemptionRequestCommand("CERT-2", null, new List<Guid> { mediaId }),
                CancellationToken.None));
    }

    [Fact]
    public async Task Approve_marks_customer_exempt_and_copies_evidence()
    {
        var (customerId, userId) = SeedCustomer();
        var mediaId = SeedOwnedMedia(userId);
        var adminId = Guid.NewGuid();

        var submitted = await SubmitHandler(userId).Handle(
            new SubmitTaxExemptionRequestCommand("CERT-123", "VAT-9", new List<Guid> { mediaId }), CancellationToken.None);

        var resolved = await ResolveHandler(adminId).Handle(
            new ResolveTaxExemptionRequestCommand(submitted.Id, Approve: true, "looks good"), CancellationToken.None);

        Assert.Equal(TaxExemptionRequestStatus.Approved, resolved.Status);

        using var db = NewContext();
        var customer = db.Customers.First(c => c.Id == customerId);
        Assert.True(customer.IsTaxExempt);
        Assert.Equal("CERT-123", customer.TaxExemptionCertificate);
        Assert.Equal("VAT-9", customer.VatId);
    }

    [Fact]
    public async Task Reject_leaves_customer_taxable_and_allows_resubmit()
    {
        var (customerId, userId) = SeedCustomer();
        var mediaId = SeedOwnedMedia(userId);

        var submitted = await SubmitHandler(userId).Handle(
            new SubmitTaxExemptionRequestCommand("CERT-1", null, new List<Guid> { mediaId }), CancellationToken.None);

        await ResolveHandler(Guid.NewGuid()).Handle(
            new ResolveTaxExemptionRequestCommand(submitted.Id, Approve: false, "insufficient"), CancellationToken.None);

        using (var db = NewContext())
            Assert.False(db.Customers.First(c => c.Id == customerId).IsTaxExempt);

        // A new request is allowed once the previous was decided (AC-TAX-003.6).
        var resubmitted = await SubmitHandler(userId).Handle(
            new SubmitTaxExemptionRequestCommand("CERT-2", null, new List<Guid> { mediaId }), CancellationToken.None);
        Assert.Equal(TaxExemptionRequestStatus.PendingReview, resubmitted.Status);
    }

    [Fact]
    public async Task Resolve_twice_conflicts()
    {
        var (_, userId) = SeedCustomer();
        var mediaId = SeedOwnedMedia(userId);

        var submitted = await SubmitHandler(userId).Handle(
            new SubmitTaxExemptionRequestCommand("CERT-1", null, new List<Guid> { mediaId }), CancellationToken.None);

        await ResolveHandler(Guid.NewGuid()).Handle(
            new ResolveTaxExemptionRequestCommand(submitted.Id, Approve: true, null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            ResolveHandler(Guid.NewGuid()).Handle(
                new ResolveTaxExemptionRequestCommand(submitted.Id, Approve: false, null), CancellationToken.None));
    }
}
