using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.TaxExemptionRequests;

/// <summary>
/// Submits a tax exemption request for the signed-in customer (AC-TAX-003.1). The request enters
/// PendingReview and the customer stays taxable until an admin approves it (AC-TAX-003.3).
/// </summary>
public record SubmitTaxExemptionRequestCommand(
    string? CertificateNumber, string? VatId, List<Guid> DocumentMediaIds) : IRequest<TaxExemptionRequestDto>;

public class SubmitTaxExemptionRequestCommandValidator : AbstractValidator<SubmitTaxExemptionRequestCommand>
{
    public SubmitTaxExemptionRequestCommandValidator()
    {
        RuleFor(x => x.CertificateNumber).MaximumLength(200);
        RuleFor(x => x.VatId).MaximumLength(64);

        // Mirrors SetCustomerTaxExemptionCommand: an exemption must be substantiated.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.CertificateNumber) || !string.IsNullOrWhiteSpace(x.VatId))
            .WithMessage("A certificate number or a VAT id is required.");

        // AC-TAX-003.1 requires one or more supporting documents.
        RuleFor(x => x.DocumentMediaIds)
            .NotEmpty().WithMessage("At least one supporting tax document is required.");
    }
}

public class SubmitTaxExemptionRequestCommandHandler
    : IRequestHandler<SubmitTaxExemptionRequestCommand, TaxExemptionRequestDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailTemplateSender _email;

    public SubmitTaxExemptionRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, IDateTimeProvider clock, IEmailTemplateSender email)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _email = email;
    }

    public async Task<TaxExemptionRequestDto> Handle(
        SubmitTaxExemptionRequestCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException("Authentication is required to submit a tax exemption request.");

        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        // One open request at a time — otherwise a customer could queue several and an admin would approve
        // a stale one (AC-TAX-003.6 only allows a new request once the previous was decided).
        var hasPending = await _db.TaxExemptionRequests
            .AsNoTracking()
            .AnyAsync(r => r.CustomerId == customer.Id && r.Status == TaxExemptionRequestStatus.PendingReview,
                cancellationToken);
        if (hasPending)
            throw new ConflictException("A tax exemption request is already awaiting review.");

        var mediaIds = (request.DocumentMediaIds ?? new List<Guid>()).Distinct().ToList();

        // Ownership check: only media this user uploaded may be attached. Media has no owner column, but the
        // DbContext stamps CreatedById from the JWT, so it doubles as one — without this, knowing any media
        // id would let a customer attach (and then read back) someone else's document.
        var ownedCount = await _db.Media
            .AsNoTracking()
            .CountAsync(m => mediaIds.Contains(m.Id) && m.CreatedById == userId, cancellationToken);
        if (ownedCount != mediaIds.Count)
            throw new NotFoundException("One or more supporting documents could not be found.");

        var now = _clock.UtcNow;
        var exemptionRequest = new TaxExemptionRequest
        {
            CustomerId = customer.Id,
            Status = TaxExemptionRequestStatus.PendingReview,
            CertificateNumber = string.IsNullOrWhiteSpace(request.CertificateNumber) ? null : request.CertificateNumber.Trim(),
            VatId = string.IsNullOrWhiteSpace(request.VatId) ? null : request.VatId.Trim(),
            SubmittedOnUtc = now,
        };

        var order = 0;
        foreach (var mediaId in mediaIds)
            exemptionRequest.Documents.Add(new TaxExemptionRequestDocument { MediaId = mediaId, DisplayOrder = order++ });

        _db.TaxExemptionRequests.Add(exemptionRequest);
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyAdminAsync(customer, exemptionRequest, mediaIds.Count, cancellationToken);

        return TaxExemptionRequestDto.From(exemptionRequest);
    }

    /// <summary>
    /// Emails the tenant's support/admin address that a new request is awaiting review — so an admin is
    /// prompted to act rather than having to notice it in the pending list. Sent to
    /// <see cref="Domain.Entities.TenantBranding.SupportEmail"/>; a no-op when that is unset. Best-effort:
    /// a mail failure must never roll back the submitted request.
    /// </summary>
    private async Task NotifyAdminAsync(Customer customer, TaxExemptionRequest request, int documentCount, CancellationToken ct)
    {
        var recipient = await _db.TenantBrandings
            .AsNoTracking()
            .Select(b => b.SupportEmail)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(recipient))
            return;

        var customerName = $"{customer.FirstName} {customer.LastName}".Trim();
        var variables = new Dictionary<string, string>
        {
            ["customerName"] = string.IsNullOrWhiteSpace(customerName) ? "A customer" : customerName,
            ["customerEmail"] = _current.Email ?? string.Empty,
            ["certificateNumber"] = request.CertificateNumber ?? "—",
            ["vatId"] = request.VatId ?? "—",
            ["documentCount"] = documentCount.ToString(),
            ["submittedOn"] = $"{request.SubmittedOnUtc:MMM d, yyyy HH:mm} UTC",
        };

        try
        {
            await _email.SendAsync("tax-exemption.submitted", recipient, null, variables, ct);
        }
        catch
        {
            // Swallowed deliberately — the request is already persisted.
        }
    }
}
