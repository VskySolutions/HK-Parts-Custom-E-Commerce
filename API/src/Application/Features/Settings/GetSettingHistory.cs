using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Settings;

public record GetSettingHistoryQuery(string? Key = null, int Take = 100)
    : IRequest<IReadOnlyList<SettingHistoryDto>>;

public class GetSettingHistoryQueryHandler
    : IRequestHandler<GetSettingHistoryQuery, IReadOnlyList<SettingHistoryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetSettingHistoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SettingHistoryDto>> Handle(GetSettingHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SettingsChangeHistory.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Key))
            query = query.Where(h => h.SettingKey == request.Key);

        var take = request.Take is <= 0 or > 500 ? 100 : request.Take;

        var history = await query
            .OrderByDescending(h => h.ChangedOnUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return history.Select(SettingHistoryDto.From).ToList();
    }
}
