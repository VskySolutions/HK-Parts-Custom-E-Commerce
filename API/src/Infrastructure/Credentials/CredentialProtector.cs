using Microsoft.AspNetCore.DataProtection;
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

    public CredentialProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("VSky.CredentialVault.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
