using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>A single downloadable-file input row for <see cref="SetProductDownloadsCommand"/>.</summary>
public record ProductDownloadInput(string Name, string? Url, int DisplayOrder = 0);

/// <summary>Replaces the downloadable files/URLs attached to a downloadable product (AC-CAT-001.4).</summary>
public record SetProductDownloadsCommand(Guid ProductId, List<ProductDownloadInput> Downloads)
    : IRequest<ProductDto>;

public class SetProductDownloadsCommandValidator : AbstractValidator<SetProductDownloadsCommand>
{
    public SetProductDownloadsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleForEach(x => x.Downloads).ChildRules(d =>
        {
            d.RuleFor(i => i.Name).NotEmpty().MaximumLength(400);
            d.RuleFor(i => i.Url).MaximumLength(2048);
        });
    }
}

public class SetProductDownloadsCommandHandler : IRequestHandler<SetProductDownloadsCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductDownloadsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetProductDownloadsCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var downloads = request.Downloads ?? new List<ProductDownloadInput>();

        foreach (var existing in product.Downloads.ToList())
            product.Downloads.Remove(existing);

        foreach (var download in downloads)
        {
            product.Downloads.Add(new ProductDownload
            {
                ProductId = product.Id,
                Name = download.Name,
                Url = download.Url,
                DisplayOrder = download.DisplayOrder,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
