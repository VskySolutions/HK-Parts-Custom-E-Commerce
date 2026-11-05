namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// eBay reference data cached for 24 hours per store (and category): business policies, item aspects,
/// item conditions and parts-compatibility support. <c>refresh</c> bypasses and repopulates the cache.
/// </summary>
public interface IEbayMetadataService
{
    Task<IReadOnlyList<EbayPolicy>> GetPoliciesAsync(Guid storeId, EbayPolicyType type, bool refresh = false, CancellationToken ct = default);
    Task<IReadOnlyList<EbayAspectDefinition>> GetAspectsAsync(Guid storeId, string categoryId, CancellationToken ct = default);
    Task<IReadOnlyList<EbayCondition>> GetConditionsAsync(Guid storeId, string categoryId, CancellationToken ct = default);
    Task<EbayCompatibilityInfo> GetCompatibilityAsync(Guid storeId, string categoryId, CancellationToken ct = default);
}
