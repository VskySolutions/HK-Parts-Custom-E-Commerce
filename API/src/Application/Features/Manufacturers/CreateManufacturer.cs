using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Manufacturers;

/// <summary>Creates a new manufacturer/brand.</summary>
public record CreateManufacturerCommand(
    string Name,
    string? Description = null,
    Guid? LogoMediaId = null,
    string? LogoUrl = null,
    string? Slug = null,
    int DisplayOrder = 0,
    bool IsEnabled = true) : IRequest<ManufacturerDto>;

public class CreateManufacturerCommandValidator : AbstractValidator<CreateManufacturerCommand>
{
    public CreateManufacturerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(220);
    }
}

public class CreateManufacturerCommandHandler : IRequestHandler<CreateManufacturerCommand, ManufacturerDto>
{
    private readonly IApplicationDbContext _db;

    public CreateManufacturerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ManufacturerDto> Handle(CreateManufacturerCommand request, CancellationToken cancellationToken)
    {
        var slug = SlugGenerator.NormalizeOptional(request.Slug);
        if (slug is not null && await _db.Manufacturers.AnyAsync(x => x.Slug == slug, cancellationToken))
            throw new ConflictException($"Another manufacturer already uses the slug '{slug}'.");

        var entity = new Manufacturer
        {
            Name = request.Name,
            Description = request.Description,
            LogoMediaId = request.LogoMediaId,
            // Going forward the logo lives in Media; only keep a raw URL when no Media asset is set.
            LogoUrl = request.LogoMediaId.HasValue ? null : request.LogoUrl,
            Slug = slug,
            DisplayOrder = request.DisplayOrder,
            IsEnabled = request.IsEnabled,
        };

        _db.Manufacturers.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ManufacturerDto.From(entity);
    }
}
