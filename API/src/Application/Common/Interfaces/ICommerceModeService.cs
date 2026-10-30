using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Resolves the tenant's commerce mode (REQ-INQ-001) from the cached <c>commerce.*</c> platform settings.
/// Every server-side guard that refuses payment in inquiry mode reads through this, so the switch is
/// enforced in one place rather than trusted from the client.
/// </summary>
public interface ICommerceModeService
{
    /// <summary>The tenant's resolved commerce settings (falls back to Standard when unconfigured).</summary>
    Task<CommerceModeSettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Shorthand for <c>(await GetAsync()).IsInquiryOnly</c>.</summary>
    Task<bool> IsInquiryOnlyAsync(CancellationToken cancellationToken = default);
}
