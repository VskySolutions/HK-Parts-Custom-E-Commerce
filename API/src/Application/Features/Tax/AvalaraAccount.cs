using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>
/// PHP "Avalara Settings" account fields (Account Number, License Key, Company Code, Service URL), edited from the
/// Tax → Avalara tab. They are stored on the active <see cref="AvalaraCredential"/> — the same row the tax client and
/// Avalara address verification read — so there is still exactly one Avalara account. The licence key is never
/// returned; leaving it blank on save keeps the key on file.
/// </summary>
public class AvalaraAccountDto
{
    public Guid? Id { get; set; }
    public string? ServiceUrl { get; set; }
    public string? AccountNumber { get; set; }
    public string? CompanyCode { get; set; }
    public bool HasLicenseKey { get; set; }
    public bool IsProduction { get; set; }

    public static AvalaraAccountDto From(AvalaraCredential? e) => e is null ? new() : new()
    {
        Id = e.Id,
        ServiceUrl = e.BaseUrl,
        AccountNumber = e.AccountId,
        CompanyCode = e.CompanyCode,
        HasLicenseKey = !string.IsNullOrWhiteSpace(e.LicenseKey),
        IsProduction = e.IsProduction,
    };

    /// <summary>The row the runtime uses: the most recently updated active credential.</summary>
    internal static Task<AvalaraCredential?> LoadActiveAsync(IApplicationDbContext db, bool tracking, CancellationToken ct)
    {
        IQueryable<AvalaraCredential> q = db.AvalaraCredentials;
        if (!tracking) q = q.AsNoTracking();
        return q.Where(c => c.Active).OrderByDescending(c => c.UpdatedOnUtc).FirstOrDefaultAsync(ct);
    }
}

public record GetAvalaraAccountQuery : IRequest<AvalaraAccountDto>;

public class GetAvalaraAccountQueryHandler : IRequestHandler<GetAvalaraAccountQuery, AvalaraAccountDto>
{
    private readonly IApplicationDbContext _db;
    public GetAvalaraAccountQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<AvalaraAccountDto> Handle(GetAvalaraAccountQuery request, CancellationToken ct)
        => AvalaraAccountDto.From(await AvalaraAccountDto.LoadActiveAsync(_db, tracking: false, ct));
}

/// <summary>Saves the PHP Avalara account fields. <paramref name="LicenseKey"/> blank = keep the key on file.</summary>
public record SaveAvalaraAccountCommand(
    string? ServiceUrl, string? AccountNumber, string? LicenseKey, string? CompanyCode) : IRequest<AvalaraAccountDto>;

public class SaveAvalaraAccountCommandValidator : AbstractValidator<SaveAvalaraAccountCommand>
{
    public SaveAvalaraAccountCommandValidator()
    {
        // PHP AvalaraSettingsRequest: account number, licence key, company code and service URL are all required.
        RuleFor(x => x.ServiceUrl).NotEmpty().WithMessage("Service URL is required.")
            .MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Service URL must be an absolute http(s) URL, e.g. https://sandbox-rest.avatax.com");
        RuleFor(x => x.AccountNumber).NotEmpty().WithMessage("Account Number is required.").MaximumLength(100);
        RuleFor(x => x.CompanyCode).NotEmpty().WithMessage("Company Code is required.").MaximumLength(100);
        RuleFor(x => x.LicenseKey).MaximumLength(500);
    }
}

public class SaveAvalaraAccountCommandHandler : IRequestHandler<SaveAvalaraAccountCommand, AvalaraAccountDto>
{
    private readonly IApplicationDbContext _db;
    public SaveAvalaraAccountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<AvalaraAccountDto> Handle(SaveAvalaraAccountCommand request, CancellationToken ct)
    {
        var existing = await AvalaraAccountDto.LoadActiveAsync(_db, tracking: true, ct);
        var licenseKey = IntegrationCredentialSupport.Norm(request.LicenseKey);

        if (licenseKey is null && string.IsNullOrWhiteSpace(existing?.LicenseKey))
            throw new Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.LicenseKey), "License Key is required."),
            });

        var url = IntegrationCredentialSupport.Norm(request.ServiceUrl)!.TrimEnd('/');
        // Avalara's live host is rest.avatax.com; anything else (sandbox-rest.avatax.com, a proxy) counts as test.
        var isProduction = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Host.Equals("rest.avatax.com", StringComparison.OrdinalIgnoreCase);

        var entity = await IntegrationCredentialSupport.UpsertAsync(
            _db.AvalaraCredentials, existing?.Id, existing?.Name ?? "Avalara", active: true, isProduction, ct);
        entity.BaseUrl = url;
        entity.AccountId = IntegrationCredentialSupport.Norm(request.AccountNumber);
        entity.CompanyCode = IntegrationCredentialSupport.Norm(request.CompanyCode);
        if (licenseKey is not null) entity.LicenseKey = licenseKey;

        await _db.SaveChangesAsync(ct);
        return AvalaraAccountDto.From(entity);
    }
}
