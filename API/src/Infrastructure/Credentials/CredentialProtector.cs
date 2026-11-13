using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Credentials;

/// <summary>
/// Column-level credential protection backed by the .NET Data Protection API. Registered as a singleton so
/// the value converter captured in the (cached) EF model always references a live protector. The purpose
/// string is shared with <see cref="CredentialVault"/> so values encrypted by either round-trip.
/// </summary>
public sealed class CredentialProtector : ICredentialProtector
{
    private readonly IDataProtector _protector;
    private readonly ILogger<CredentialProtector> _logger;

    public CredentialProtector(IDataProtectionProvider provider, ILogger<CredentialProtector> logger)
    {
        _protector = provider.CreateProtector("VSky.CredentialVault.v1");
        _logger = logger;
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);

    public string? TryUnprotect(string ciphertext)
    {
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (CryptographicException ex)
        {
            // Never log the ciphertext itself. The message names the missing key id, which is what the
            // operator needs to restore the key file or re-enter the secret.
            _logger.LogWarning(
                "A stored credential secret could not be decrypted and is treated as not set: {Reason} " +
                "Re-enter it in the admin (Integrations / SMTP), or restore the Data Protection key file it was encrypted with.",
                ex.Message);
            return null;
        }
    }
}
