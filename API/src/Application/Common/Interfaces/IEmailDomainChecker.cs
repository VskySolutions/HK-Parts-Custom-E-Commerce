namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Guest checkout U-11: whether an email's domain can receive mail (it has MX records, or at least an address).
/// A DNS failure or timeout answers "yes": checkout is never blocked because a lookup could not be made.
/// </summary>
public interface IEmailDomainChecker
{
    Task<bool> CanReceiveMailAsync(string email, CancellationToken cancellationToken = default);
}
