using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.SmtpAccounts;

/// <summary>Sends a test email through a stored SMTP account (AC-TEN test-send).</summary>
public record TestSendSmtpAccountCommand(Guid Id, string ToEmail) : IRequest<ConnectivityTestResult>;

public class TestSendSmtpAccountCommandValidator : AbstractValidator<TestSendSmtpAccountCommand>
{
    public TestSendSmtpAccountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ToEmail).NotEmpty().EmailAddress();
    }
}

public class TestSendSmtpAccountCommandHandler : IRequestHandler<TestSendSmtpAccountCommand, ConnectivityTestResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICredentialVault _vault;
    private readonly ISmtpTester _tester;

    public TestSendSmtpAccountCommandHandler(IApplicationDbContext db, ICredentialVault vault, ISmtpTester tester)
    {
        _db = db;
        _vault = vault;
        _tester = tester;
    }

    public async Task<ConnectivityTestResult> Handle(TestSendSmtpAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _db.SmtpAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("SmtpAccount", request.Id);

        var password = string.IsNullOrEmpty(account.EncryptedPassword) ? null : _vault.Decrypt(account.EncryptedPassword);

        return await _tester.TestSendAsync(new SmtpTestRequest
        {
            Host = account.Host,
            Port = account.Port,
            Username = account.Username,
            Password = password,
            FromName = account.FromName,
            FromEmail = account.FromEmail,
            EncryptionMode = account.EncryptionMode,
            AuthMethod = account.AuthMethod,
            ToEmail = request.ToEmail,
        }, cancellationToken);
    }
}
