using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.SmtpAccounts;

/// <summary>Updates an SMTP account. A blank password leaves the stored one unchanged.</summary>
public record UpdateSmtpAccountCommand(
    Guid Id,
    string DisplayName,
    string Host,
    int Port,
    string? Username,
    string? Password,
    string FromName,
    string FromEmail,
    SmtpEncryptionMode EncryptionMode,
    SmtpAuthMethod AuthMethod,
    NotificationCategory? Category,
    bool Enabled) : IRequest<SmtpAccountDto>;

public class UpdateSmtpAccountCommandValidator : AbstractValidator<UpdateSmtpAccountCommand>
{
    public UpdateSmtpAccountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Host).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.FromName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FromEmail).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Username).MaximumLength(255);
    }
}

public class UpdateSmtpAccountCommandHandler : IRequestHandler<UpdateSmtpAccountCommand, SmtpAccountDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICredentialVault _vault;

    public UpdateSmtpAccountCommandHandler(IApplicationDbContext db, ICredentialVault vault)
    {
        _db = db;
        _vault = vault;
    }

    public async Task<SmtpAccountDto> Handle(UpdateSmtpAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _db.SmtpAccounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("SmtpAccount", request.Id);

        account.DisplayName = request.DisplayName;
        account.Host = request.Host;
        account.Port = request.Port;
        account.Username = request.Username;
        account.FromName = request.FromName;
        account.FromEmail = request.FromEmail;
        account.EncryptionMode = request.EncryptionMode;
        account.AuthMethod = request.AuthMethod;
        account.Category = request.Category;
        account.Enabled = request.Enabled;

        if (!string.IsNullOrEmpty(request.Password))
            account.EncryptedPassword = _vault.Encrypt(request.Password);

        await SmtpCategoryRules.EnsureSingleActivePerCategoryAsync(_db, account, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return SmtpAccountDto.From(account);
    }
}

/// <summary>Enforces "at most one enabled SMTP account per notification category".</summary>
internal static class SmtpCategoryRules
{
    public static async Task EnsureSingleActivePerCategoryAsync(IApplicationDbContext db, SmtpAccount account, CancellationToken ct)
    {
        if (!account.Enabled || account.Category is null)
            return;

        var others = await db.SmtpAccounts
            .Where(a => a.Id != account.Id && a.Category == account.Category && a.Enabled)
            .ToListAsync(ct);

        foreach (var other in others)
            other.Enabled = false;
    }
}
