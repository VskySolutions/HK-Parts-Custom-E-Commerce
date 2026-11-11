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

    /// <summary>
    /// Like <see cref="Unprotect"/>, but returns null (and logs a warning) when the value cannot be
    /// decrypted — typically because it was encrypted under a key this deployment's key ring does not hold
    /// (a DB copied from another machine, or a key folder wiped by a redeploy). Reading such a secret as
    /// "not set" lets the admin re-enter it instead of every request that touches the row failing.
    /// </summary>
    string? TryUnprotect(string ciphertext);
}
