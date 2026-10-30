using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsBanners;

/// <summary>Soft-deletes a banner (idempotent).</summary>
public record DeleteCmsBannerCommand(Guid Id) : IRequest;

public class DeleteCmsBannerCommandHandler : IRequestHandler<DeleteCmsBannerCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCmsBannerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCmsBannerCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSBanners
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.CMSBanners.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
