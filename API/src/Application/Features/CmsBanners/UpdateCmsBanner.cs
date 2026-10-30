using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsBanners;

/// <summary>Updates an existing promotional banner/slide.</summary>
public record UpdateCmsBannerCommand(
    Guid Id,
    string Title,
    string DisplayLocation,
    string? Subtitle = null,
    Guid? ImageMediaId = null,
    string? LinkUrl = null,
    string? CtaLabel = null,
    DateTime? StartsOnUtc = null,
    DateTime? EndsOnUtc = null,
    int DisplayOrder = 0,
    bool IsEnabled = true) : IRequest<CmsBannerDto>;

public class UpdateCmsBannerCommandValidator : AbstractValidator<UpdateCmsBannerCommand>
{
    public UpdateCmsBannerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayLocation).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EndsOnUtc).GreaterThan(x => x.StartsOnUtc!.Value)
            .When(x => x.StartsOnUtc.HasValue && x.EndsOnUtc.HasValue)
            .WithMessage("EndsOnUtc must be after StartsOnUtc.");
    }
}

public class UpdateCmsBannerCommandHandler : IRequestHandler<UpdateCmsBannerCommand, CmsBannerDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateCmsBannerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsBannerDto> Handle(UpdateCmsBannerCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSBanners
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSBanner), request.Id);

        entity.Title = request.Title;
        entity.Subtitle = request.Subtitle;
        entity.ImageMediaId = request.ImageMediaId;
        entity.LinkUrl = request.LinkUrl;
        entity.CtaLabel = request.CtaLabel;
        entity.DisplayLocation = request.DisplayLocation;
        entity.StartsOnUtc = request.StartsOnUtc;
        entity.EndsOnUtc = request.EndsOnUtc;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsEnabled = request.IsEnabled;

        await _db.SaveChangesAsync(cancellationToken);
        return CmsBannerDto.From(entity);
    }
}
