using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// Submits an inquiry (quote request) instead of paying (REQ-INQ-001). Public by design — guests are the
/// point of a lead form — so it is reCAPTCHA-gated on its own form type rather than reusing guest checkout.
/// </summary>
public record SubmitInquiryCommand(SubmitInquiryRequest Request) : IRequest<InquiryResult>;

public class SubmitInquiryCommandValidator : AbstractValidator<SubmitInquiryCommand>
{
    public SubmitInquiryCommandValidator()
    {
        // Contact details are the deliverable here: an inquiry nobody can reply to is worthless. The postal
        // fields are deliberately not required — a contact-only tenant collects none of them.
        RuleFor(x => x.Request.Contact.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Contact.LastName).MaximumLength(100);
        RuleFor(x => x.Request.Contact.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Request.Contact.PhoneNumber).NotEmpty().MaximumLength(40)
            .WithMessage("A phone number is required so we can respond to your request.");

        RuleFor(x => x.Request.Message).MaximumLength(4000);
        RuleFor(x => x.Request.CompanyName).MaximumLength(200);

        RuleFor(x => x.Request.PreferredContact)
            .Must(v => string.IsNullOrWhiteSpace(v) || Enum.TryParse<ContactPreference>(v, ignoreCase: true, out _))
            .WithMessage("Preferred contact must be Email, Phone or WhatsApp.");
    }
}

public class SubmitInquiryCommandHandler : IRequestHandler<SubmitInquiryCommand, InquiryResult>
{
    private readonly ICheckoutOrchestrator _orchestrator;
    private readonly IRecaptchaVerifier _recaptcha;

    public SubmitInquiryCommandHandler(ICheckoutOrchestrator orchestrator, IRecaptchaVerifier recaptcha)
    {
        _orchestrator = orchestrator;
        _recaptcha = recaptcha;
    }

    public async Task<InquiryResult> Handle(SubmitInquiryCommand request, CancellationToken cancellationToken)
    {
        await _recaptcha.VerifyOrThrowAsync(RecaptchaFormType.Inquiry, request.Request.RecaptchaToken, cancellationToken);
        return await _orchestrator.SubmitInquiryAsync(request.Request, cancellationToken);
    }
}
