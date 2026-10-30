using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.TaxExemptionRequests;

/// <summary>
/// Approves or rejects a pending tax exemption request (AC-TAX-003.4).
/// <para>
/// Approving is what makes a customer tax-exempt: it sets <see cref="Customer.IsTaxExempt"/> and copies the
/// approved certificate/VAT id onto the customer record (AC-TAX-003.5), which is the state the checkout tax
/// step already reads — so no tax-layer change is needed for exemption to take effect. Rejecting leaves the
/// customer taxable and frees them to submit again (AC-TAX-003.6).
/// </para>
/// </summary>
public record ResolveTaxExemptionRequestCommand(Guid RequestId, bool Approve, string? AdminNote)
    : IRequest<TaxExemptionRequestDto>;

public class ResolveTaxExemptionRequestCommandValidator : AbstractValidator<ResolveTaxExemptionRequestCommand>
{
    public ResolveTaxExemptionRequestCommandValidator()
    {
        RuleFor(x => x.AdminNote).MaximumLength(1024);
    }
}

public class ResolveTaxExemptionRequestCommandHandler
    : IRequestHandler<ResolveTaxExemptionRequestCommand, TaxExemptionRequestDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailTemplateSender _email;

    public ResolveTaxExemptionRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, IDateTimeProvider clock, IEmailTemplateSender email)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _email = email;
    }

    public async Task<TaxExemptionRequestDto> Handle(
        ResolveTaxExemptionRequestCommand request, CancellationToken cancellationToken)
    {
        var exemptionRequest = await _db.TaxExemptionRequests
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(TaxExemptionRequest), request.RequestId);

        if (exemptionRequest.Status != TaxExemptionRequestStatus.PendingReview)
            throw new ConflictException($"This request is already {exemptionRequest.Status} and cannot be reviewed again.");

        var customer = await _db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == exemptionRequest.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), exemptionRequest.CustomerId);

        var now = _clock.UtcNow;
        exemptionRequest.AdminNote = string.IsNullOrWhiteSpace(request.AdminNote) ? null : request.AdminNote.Trim();
        exemptionRequest.ReviewedOnUtc = now;
        exemptionRequest.ReviewedById = _current.UserId;

        if (request.Approve)
        {
            exemptionRequest.Status = TaxExemptionRequestStatus.Approved;

            // The approved evidence becomes the customer's effective exemption (AC-TAX-003.5).
            customer.IsTaxExempt = true;
            customer.TaxExemptionCertificate = exemptionRequest.CertificateNumber;
            customer.VatId = exemptionRequest.VatId;
        }
        else
        {
            exemptionRequest.Status = TaxExemptionRequestStatus.Rejected;
            // A rejection must never leave a stale exemption behind (AC-TAX-003.6).
            customer.IsTaxExempt = false;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await NotifyAsync(customer, exemptionRequest, request.Approve, cancellationToken);

        return TaxExemptionRequestDto.From(exemptionRequest);
    }

    private async Task NotifyAsync(Customer customer, TaxExemptionRequest request, bool approved, CancellationToken ct)
    {
        var email = customer.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
            return;

        var customerName = $"{customer.FirstName} {customer.LastName}".Trim();
        var variables = new Dictionary<string, string>
        {
            ["customerName"] = string.IsNullOrWhiteSpace(customerName) ? "there" : customerName,
            ["status"] = request.Status.ToString(),
            ["adminNote"] = request.AdminNote ?? string.Empty,
            ["certificateNumber"] = request.CertificateNumber ?? string.Empty,
            ["vatId"] = request.VatId ?? string.Empty,
        };

        // Best-effort: a mail failure must not roll back a completed review.
        try
        {
            await _email.SendAsync(
                approved ? "tax-exemption.approved" : "tax-exemption.rejected",
                email, customerName, variables, ct);
        }
        catch
        {
            // Swallowed deliberately — the decision is already persisted.
        }
    }
}
