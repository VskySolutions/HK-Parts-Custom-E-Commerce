using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.SmtpAccounts;

public record CreateSmtpAccountCommand(
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

public class CreateSmtpAccountCommandValidator : AbstractValidator<CreateSmtpAccountCommand>
{
    public CreateSmtpAccountCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Host).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.FromName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FromEmail).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Username).MaximumLength(255);
    }
}

public class CreateSmtpAccountCommandHandler : IRequestHandler<CreateSmtpAccountCommand, SmtpAccountDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICredentialVault _vault;

    public CreateSmtpAccountCommandHandler(IApplicationDbContext db, ICredentialVault vault)
    {
        _db = db;
        _vault = vault;
    }

    public async Task<SmtpAccountDto> Handle(CreateSmtpAccountCommand request, CancellationToken cancellationToken)
    {
        var account = new SmtpAccount
        {
            DisplayName = request.DisplayName,
            Host = request.Host,
            Port = request.Port,
            Username = request.Username,
            FromName = request.FromName,
            FromEmail = request.FromEmail,
            EncryptionMode = request.EncryptionMode,
            AuthMethod = request.AuthMethod,
            Category = request.Category,
            Enabled = request.Enabled,
            EncryptedPassword = string.IsNullOrEmpty(request.Password) ? null : _vault.Encrypt(request.Password),
        };

        _db.SmtpAccounts.Add(account);
        await SmtpCategoryRules.EnsureSingleActivePerCategoryAsync(_db, account, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return SmtpAccountDto.From(account);
    }
}
