using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>
/// Replaces a product's tags with the requested set of names, creating any tag that does not yet
/// exist (case-insensitive match) before reconciling the mappings (REQ-CAT-008).
/// </summary>
public record SetProductTagsCommand(Guid ProductId, List<string> TagNames) : IRequest<ProductDto>;

public class SetProductTagsCommandHandler : IRequestHandler<SetProductTagsCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductTagsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetProductTagsCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // Normalise: trim, drop blanks, de-duplicate case-insensitively (keeping first spelling seen).
        var names = (request.TagNames ?? new List<string>())
            .Select(n => (n ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .GroupBy(n => n.ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        // Resolve each name to an existing tag or create a new one.
        var desiredTags = new List<ProductTag>();
        foreach (var name in names)
        {
            var lowered = name.ToLower();
            var tag = await _db.ProductTags.FirstOrDefaultAsync(t => t.Name.ToLower() == lowered, cancellationToken);
            if (tag is null)
            {
                tag = new ProductTag { Name = name, Slug = Slugify(name) };
                _db.ProductTags.Add(tag);
            }
            desiredTags.Add(tag);
        }

        var desiredIds = desiredTags.Select(t => t.Id).ToHashSet();
        foreach (var mapping in product.Tags.Where(m => !desiredIds.Contains(m.ProductTagId)).ToList())
            product.Tags.Remove(mapping);

        var assigned = product.Tags.Select(m => m.ProductTagId).ToHashSet();
        foreach (var tag in desiredTags.Where(t => !assigned.Contains(t.Id)))
            product.Tags.Add(new ProductTagMapping { ProductId = product.Id, ProductTag = tag });

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }

    private static string Slugify(string value)
    {
        var slug = new string(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
