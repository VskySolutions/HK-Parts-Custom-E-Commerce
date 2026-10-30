using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Localization;

// ---- Queries -----------------------------------------------------------------

/// <summary>Lists all configured languages (admin), ordered for display (AC-STF-004.1).</summary>
public record ListLanguagesQuery : IRequest<List<LanguageDto>>;

public class ListLanguagesQueryHandler : IRequestHandler<ListLanguagesQuery, List<LanguageDto>>
{
    private readonly IApplicationDbContext _db;
    public ListLanguagesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<LanguageDto>> Handle(ListLanguagesQuery request, CancellationToken cancellationToken)
    {
        var languages = await _db.Languages.AsNoTracking()
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.DisplayOrder).ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);
        return languages.Select(LanguageDto.From).ToList();
    }
}

/// <summary>Lists the enabled languages for the storefront selector, default first (AC-STF-004.2).</summary>
public record GetActiveLanguagesQuery : IRequest<List<StorefrontLanguageDto>>;

public class GetActiveLanguagesQueryHandler : IRequestHandler<GetActiveLanguagesQuery, List<StorefrontLanguageDto>>
{
    private readonly IApplicationDbContext _db;
    public GetActiveLanguagesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<StorefrontLanguageDto>> Handle(GetActiveLanguagesQuery request, CancellationToken cancellationToken)
    {
        var languages = await _db.Languages.AsNoTracking()
            .Where(l => l.IsEnabled)
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.DisplayOrder).ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);
        return languages.Select(StorefrontLanguageDto.From).ToList();
    }
}

// ---- Commands ----------------------------------------------------------------

/// <summary>Adds a supported language (AC-STF-004.1).</summary>
public record CreateLanguageCommand(string Code, string Name, string? NativeName = null, bool IsEnabled = true, int DisplayOrder = 0)
    : IRequest<LanguageDto>;

public class CreateLanguageCommandValidator : AbstractValidator<CreateLanguageCommand>
{
    public CreateLanguageCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

public class CreateLanguageCommandHandler : IRequestHandler<CreateLanguageCommand, LanguageDto>
{
    private readonly IApplicationDbContext _db;
    public CreateLanguageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<LanguageDto> Handle(CreateLanguageCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToLowerInvariant();
        if (await _db.Languages.AnyAsync(l => l.Code == code, cancellationToken))
            throw new ConflictException($"Language '{code}' already exists.");

        var isFirst = !await _db.Languages.AnyAsync(cancellationToken);
        var language = new Language
        {
            Code = code,
            Name = request.Name,
            NativeName = request.NativeName,
            IsEnabled = request.IsEnabled,
            IsDefault = isFirst, // the first language added becomes the default
            DisplayOrder = request.DisplayOrder,
        };
        _db.Languages.Add(language);
        await _db.SaveChangesAsync(cancellationToken);
        return LanguageDto.From(language);
    }
}

/// <summary>Updates a language's name/enabled/order (AC-STF-004.1).</summary>
public record UpdateLanguageCommand(Guid Id, string Name, string? NativeName, bool IsEnabled, int DisplayOrder)
    : IRequest<LanguageDto>;

public class UpdateLanguageCommandHandler : IRequestHandler<UpdateLanguageCommand, LanguageDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateLanguageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<LanguageDto> Handle(UpdateLanguageCommand request, CancellationToken cancellationToken)
    {
        var language = await _db.Languages.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Language), request.Id);

        // The default language cannot be disabled (it is the fallback).
        if (language.IsDefault && !request.IsEnabled)
            throw new ConflictException("The default language cannot be disabled.");

        language.Name = request.Name;
        language.NativeName = request.NativeName;
        language.IsEnabled = request.IsEnabled;
        language.DisplayOrder = request.DisplayOrder;
        await _db.SaveChangesAsync(cancellationToken);
        return LanguageDto.From(language);
    }
}

/// <summary>Sets the default (fallback) language (AC-STF-004.1/004.4).</summary>
public record SetDefaultLanguageCommand(Guid Id) : IRequest<LanguageDto>;

public class SetDefaultLanguageCommandHandler : IRequestHandler<SetDefaultLanguageCommand, LanguageDto>
{
    private readonly IApplicationDbContext _db;
    public SetDefaultLanguageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<LanguageDto> Handle(SetDefaultLanguageCommand request, CancellationToken cancellationToken)
    {
        var languages = await _db.Languages.ToListAsync(cancellationToken);
        var target = languages.FirstOrDefault(l => l.Id == request.Id)
            ?? throw new NotFoundException(nameof(Language), request.Id);

        foreach (var language in languages)
            language.IsDefault = language.Id == target.Id;
        target.IsEnabled = true; // the default must be enabled

        await _db.SaveChangesAsync(cancellationToken);
        return LanguageDto.From(target);
    }
}

/// <summary>Removes a language (the default cannot be removed).</summary>
public record DeleteLanguageCommand(Guid Id) : IRequest;

public class DeleteLanguageCommandHandler : IRequestHandler<DeleteLanguageCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteLanguageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteLanguageCommand request, CancellationToken cancellationToken)
    {
        var language = await _db.Languages.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);
        if (language is null)
            return;
        if (language.IsDefault)
            throw new ConflictException("The default language cannot be removed; set another default first.");

        _db.Languages.Remove(language);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
