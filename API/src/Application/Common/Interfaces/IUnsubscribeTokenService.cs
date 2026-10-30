namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Generates and validates signed, self-contained marketing-email unsubscribe tokens. A token encodes the
/// recipient's email, is tamper-proof, and requires no authentication to redeem (WO-87 AC-ENT-006.2). The
/// implementation is backed by ASP.NET Core Data Protection: the protected (authenticated-encrypted) payload
/// of the email doubles as the token, so validation is constant-time and no separate secret is needed.
/// </summary>
public interface IUnsubscribeTokenService
{
    /// <summary>Produces an opaque, URL-safe token that encodes <paramref name="email"/>.</summary>
    string Generate(string email);

    /// <summary>
    /// Validates a token and recovers the encoded email. Returns <c>false</c> (never throws) for a missing,
    /// malformed, or tampered token so callers can render a friendly page instead of an error.
    /// </summary>
    bool TryValidate(string token, out string email);
}
