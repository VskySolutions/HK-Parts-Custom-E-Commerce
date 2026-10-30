namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Flags orders whose payment authorization hold has lapsed without capture (WO-34, AC-PAY-002.4):
/// voids the expired hold locally and raises an admin alert for manual review. Intended to be driven by
/// a scheduled task. Returns the number of authorizations flagged.
/// </summary>
public interface IExpiredAuthorizationScanner
{
    Task<int> ScanAsync(CancellationToken ct = default);
}
