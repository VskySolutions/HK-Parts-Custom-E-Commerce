namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Low-level symmetric protector for credential columns, backed by the .NET Data Protection API. It
/// depends only on the deployment key ring — never on the DbContext — so it is safe to inject into
/// <c>AppDbContext</c> (the encrypted-column value converter uses it) without creating a DI cycle.
/// </summary>
public interface ICredentialProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
