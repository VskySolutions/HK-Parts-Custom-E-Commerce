using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Inquiries;

/// <summary>
/// Emails the buyer a quote for their inquiry (REQ-INQ-001) and moves it to <c>Quoted</c>. The amount is
/// typed by the sales team rather than derived: the whole reason a product is quote-only is that its price
/// is negotiated, so the indicative total on the request is a starting point, not the answer.
/// </summary>
public record SendInquiryQuoteCommand(Guid Id, decimal Amount, string? Note) : IRequest<InquiryDto>;

public class SendInquiryQuoteCommandValidator : AbstractValidator<SendInquiryQuoteCommand>
{
    public SendInquiryQuoteCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public class SendInquiryQuoteCommandHandler : IRequestHandler<SendInquiryQuoteCommand, InquiryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailTemplateSender _templates;
    private readonly IDateTimeProvider _clock;

    public SendInquiryQuoteCommandHandler(
        IApplicationDbContext db, IEmailTemplateSender templates, IDateTimeProvider clock)
    {
        _db = db;
        _templates = templates;
        _clock = clock;
    }

    public async Task<InquiryDto> Handle(SendInquiryQuoteCommand request, CancellationToken cancellationToken)
    {
        var inquiry = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .Include(o => o.AssignedStore)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.IsInquiry, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.Id);

        var toEmail = inquiry.ContactEmail;
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ConflictException("This inquiry has no contact email to send a quote to.");

        var contactName = inquiry.ContactName;
        await _templates.SendAsync(
            "inquiry.quote",
            toEmail,
            string.IsNullOrWhiteSpace(contactName) ? null : contactName,
            new Dictionary<string, string>
            {
                ["customerName"] = string.IsNullOrWhiteSpace(contactName) ? "there" : contactName!,
                ["inquiryNumber"] = inquiry.OrderNumber,
                ["quoteAmount"] = $"{inquiry.CurrencyCode} {request.Amount:0.00}",
                ["quoteNote"] = request.Note ?? string.Empty,
            },
            cancellationToken);

        inquiry.InquiryStatus = InquiryStatus.Quoted;
        inquiry.QuotedOnUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return InquiryDto.From(inquiry);
    }
}
