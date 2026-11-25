namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Signed, expiring tokens that open one guest order without signing in (guest checkout U-60/U-61, N-04).
/// The token encodes only the order id; it is tamper-proof and time-limited, so no lookup table is needed.
/// </summary>
public interface IGuestOrderTokenService
{
    /// <summary>A URL-safe token that opens <paramref name="orderId"/> until <paramref name="lifetime"/> passes.</summary>
    string Generate(Guid orderId, TimeSpan lifetime);

    /// <summary>False (never throws) for a missing, malformed, tampered or expired token.</summary>
    bool TryValidate(string? token, out Guid orderId);
}
