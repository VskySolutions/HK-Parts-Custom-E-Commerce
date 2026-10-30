using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsCategoryConfig;

/// <summary>
/// Returns the dynamic page configuration for a category (WO-99), with its banner image URL, YMAL collection
/// name and resolved pinned product rows. When the category has no config row yet, an empty/default DTO
/// (carrying just the category id) is returned so the admin form can still render.
/// </summary>
public record GetCategoryPageConfigQuery(Guid CategoryId) : IRequest<CmsCategoryPageConfigDto>;

public class GetCategoryPageConfigQueryHandler : IRequestHandler<GetCategoryPageConfigQuery, CmsCategoryPageConfigDto>
{
    private readonly IApplicationDbContext _db;

    public GetCategoryPageConfigQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsCategoryPageConfigDto> Handle(GetCategoryPageConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _db.CMSCategoryPageConfigs
            .AsNoTracking()
            .WithDetails()
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId, cancellationToken);

        return config is null
            ? CmsCategoryPageConfigDto.Empty(request.CategoryId)
            : CmsCategoryPageConfigDto.From(config);
    }
}
