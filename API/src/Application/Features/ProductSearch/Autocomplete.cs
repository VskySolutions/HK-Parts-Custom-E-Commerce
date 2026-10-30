using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.ProductSearch;

/// <summary>
/// Returns autocomplete suggestions for a partial search term: matching published product names and
/// enabled category names, each capped at <paramref name="Limit"/> (REQ-STF-002, AC-STF-002.2).
/// </summary>
public record AutocompleteQuery(string Query, int Limit = 10) : IRequest<AutocompleteResultDto>;

public class AutocompleteQueryHandler : IRequestHandler<AutocompleteQuery, AutocompleteResultDto>
{
    private const int MaxLimit = 50;

    private readonly IApplicationDbContext _db;

    public AutocompleteQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<AutocompleteResultDto> Handle(AutocompleteQuery request, CancellationToken cancellationToken)
    {
        var term = request.Query?.Trim();
        if (string.IsNullOrEmpty(term))
            return new AutocompleteResultDto();

        var limit = request.Limit < 1 ? 10 : Math.Min(request.Limit, MaxLimit);

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsPublished && p.Name.Contains(term))
            .Select(p => p.Name)
            .Distinct()
            .OrderBy(n => n)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.IsEnabled && c.Name.Contains(term))
            .Select(c => c.Name)
            .Distinct()
            .OrderBy(n => n)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new AutocompleteResultDto
        {
            Products = products,
            Categories = categories,
        };
    }
}
