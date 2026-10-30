using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.ApiKeys;

/// <summary>Lists API keys (masked), newest first.</summary>
public record ListApiKeysQuery : IRequest<IReadOnlyList<ApiKeyDto>>;

public class ListApiKeysQueryHandler : IRequestHandler<ListApiKeysQuery, IReadOnlyList<ApiKeyDto>>
{
    private readonly IApplicationDbContext _db;

    public ListApiKeysQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApiKeyDto>> Handle(ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        var keys = await _db.ApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return keys.Select(ApiKeyDto.From).ToList();
    }
}
