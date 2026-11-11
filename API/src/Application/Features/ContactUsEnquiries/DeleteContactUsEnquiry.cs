using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.ContactUsEnquiries;

/// <summary>Soft-deletes contact us enquiry.</summary>
public record DeleteContactUsEnquiryCommand(Guid Id) : IRequest;

public class DeleteContactUsEnquiryCommandHandler : IRequestHandler<DeleteContactUsEnquiryCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteContactUsEnquiryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteContactUsEnquiryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ContactUsEnquiries.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.ContactUsEnquiries.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
