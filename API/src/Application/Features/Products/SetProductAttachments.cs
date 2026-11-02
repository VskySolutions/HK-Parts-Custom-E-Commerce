using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>One attachment row for <see cref="SetProductAttachmentsCommand"/>; list order = display order.</summary>
public record ProductAttachmentInput(Guid MediaId, string? Title);

/// <summary>
/// Replaces the public files attached to a product (manuals, spec sheets…). Each row references a
/// committed central Media asset; rows are matched by MediaId so unchanged attachments keep their id.
/// Removing an attachment leaves the Media asset in the library.
/// </summary>
public record SetProductAttachmentsCommand(Guid ProductId, List<ProductAttachmentInput> Attachments)
    : IRequest<List<ProductAttachmentDto>>;

public class SetProductAttachmentsCommandValidator : AbstractValidator<SetProductAttachmentsCommand>
{
    public SetProductAttachmentsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Attachments).NotNull();
        RuleForEach(x => x.Attachments).ChildRules(a =>
        {
            a.RuleFor(i => i.MediaId).NotEmpty();
            a.RuleFor(i => i.Title).MaximumLength(300);
        });
        RuleFor(x => x.Attachments)
            .Must(list => list == null || list.Select(a => a.MediaId).Distinct().Count() == list.Count)
            .WithMessage("The same file can only be attached once.");
    }
}

public class SetProductAttachmentsCommandHandler
    : IRequestHandler<SetProductAttachmentsCommand, List<ProductAttachmentDto>>
{
    private readonly IApplicationDbContext _db;

    public SetProductAttachmentsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<ProductAttachmentDto>> Handle(SetProductAttachmentsCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var inputs = request.Attachments ?? new List<ProductAttachmentInput>();
        var mediaIds = inputs.Select(a => a.MediaId).ToList();
        var media = await _db.Media
            .Where(m => mediaIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);
        var missing = mediaIds.FirstOrDefault(id => !media.ContainsKey(id));
        if (missing != Guid.Empty)
            throw new NotFoundException(nameof(Domain.Entities.Media), missing);

        var keep = mediaIds.ToHashSet();
        foreach (var row in product.Attachments.Where(a => !keep.Contains(a.MediaId)).ToList())
            _db.ProductAttachments.Remove(row);

        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim();
            var row = product.Attachments.FirstOrDefault(a => a.MediaId == input.MediaId);
            if (row is null)
            {
                row = new ProductAttachment { ProductId = product.Id, MediaId = input.MediaId };
                product.Attachments.Add(row);
            }
            row.Title = title;
            row.DisplayOrder = i;
            row.Media = media[input.MediaId];
        }

        await _db.SaveChangesAsync(cancellationToken);

        return product.Attachments
            .Where(a => keep.Contains(a.MediaId))
            .OrderBy(a => a.DisplayOrder)
            .Select(ProductAttachmentDto.From)
            .ToList();
    }
}
