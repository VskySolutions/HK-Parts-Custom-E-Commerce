using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Inquiries;

/// <summary>
/// Moves an inquiry along its sales pipeline and/or records internal notes (REQ-INQ-001). Conversion is a
/// separate command — this one never turns an inquiry into an order.
/// </summary>
public record UpdateInquiryCommand(Guid Id, string? InquiryStatus, string? InternalNotes) : IRequest<InquiryDto>;

public class UpdateInquiryCommandValidator : AbstractValidator<UpdateInquiryCommand>
{
    public UpdateInquiryCommandValidator()
    {
        RuleFor(x => x.InquiryStatus)
            .Must(v => string.IsNullOrWhiteSpace(v) || Enum.TryParse<InquiryStatus>(v, ignoreCase: true, out _))
            .WithMessage("Unknown inquiry status.");

        RuleFor(x => x.InternalNotes).MaximumLength(4000);
    }
}

public class UpdateInquiryCommandHandler : IRequestHandler<UpdateInquiryCommand, InquiryDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateInquiryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<InquiryDto> Handle(UpdateInquiryCommand request, CancellationToken cancellationToken)
    {
        var inquiry = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .Include(o => o.AssignedStore)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.IsInquiry, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.Id);

        if (!string.IsNullOrWhiteSpace(request.InquiryStatus))
        {
            var status = Enum.Parse<InquiryStatus>(request.InquiryStatus, ignoreCase: true);

            // Converted is reached only by ConvertInquiryToOrder, which has to create the order alongside it.
            if (status == Domain.Enums.InquiryStatus.Converted)
                throw new ConflictException("Use the convert action to turn an inquiry into an order.");

            inquiry.InquiryStatus = status;
        }

        if (request.InternalNotes is not null)
            inquiry.InternalNotes = string.IsNullOrWhiteSpace(request.InternalNotes) ? null : request.InternalNotes.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        return InquiryDto.From(inquiry);
    }
}
