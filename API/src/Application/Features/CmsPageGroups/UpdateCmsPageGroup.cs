using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsPageGroups;

/// <summary>Updates an existing CMS page group.</summary>
public record UpdateCmsPageGroupCommand(
    Guid Id,
    string Name,
    string? Slug = null,
    int DisplayOrder = 0) : IRequest<CmsPageGroupDto>;

public class UpdateCmsPageGroupCommandValidator : AbstractValidator<UpdateCmsPageGroupCommand>
{
    public UpdateCmsPageGroupCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(220);
    }
}

public class UpdateCmsPageGroupCommandHandler : IRequestHandler<UpdateCmsPageGroupCommand, CmsPageGroupDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateCmsPageGroupCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsPageGroupDto> Handle(UpdateCmsPageGroupCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSPageGroups
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSPageGroup), request.Id);

        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);

        if (await _db.CMSPageGroups.AnyAsync(g => g.Slug == slug && g.Id != request.Id, cancellationToken))
            throw new ConflictException($"A CMS page group with slug '{slug}' already exists.");

        entity.Name = request.Name;
        entity.Slug = slug;
        entity.DisplayOrder = request.DisplayOrder;

        await _db.SaveChangesAsync(cancellationToken);
        return CmsPageGroupDto.From(entity);
    }
}
