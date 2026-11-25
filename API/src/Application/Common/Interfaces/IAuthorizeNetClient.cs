namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Authorize.Net operations that are not part of the generic payment-adapter contract. Today: the admin
/// "Test" button (authenticateTestRequest, AN-41).
/// </summary>
public interface IAuthorizeNetClient
{
    /// <summary>Authenticates one stored credential row against its endpoint and reports the gateway's answer.</summary>
    Task<GatewayTestResult> TestCredentialAsync(Guid credentialId, CancellationToken ct);
}

/// <summary>Result of a gateway credential test: connected, or the gateway's error code and text.</summary>
public class GatewayTestResult
{
    public bool Connected { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    /// <summary>"Sandbox" or "Production", from the endpoint actually called.</summary>
    public string? Environment { get; set; }
    public long ElapsedMs { get; set; }

    /// <summary>Locations on the account, when the gateway has them (Square). Null otherwise.</summary>
    public List<GatewayLocation>? Locations { get; set; }
}

/// <summary>A gateway location (e.g. a Square location) listed by a credential test, to pick the Location ID from.</summary>
public class GatewayLocation
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Status { get; set; }
    public string? Address { get; set; }
    public string? Currency { get; set; }
}

/// <summary>
/// PHP parity, Square "View Locations": signs in with one stored Square credential and lists the account's locations
/// (the test result's <see cref="GatewayTestResult.Locations"/>), so the admin can check the token and copy the Location ID.
/// </summary>
public interface ISquareClient
{
    Task<GatewayTestResult> ListLocationsAsync(Guid credentialId, CancellationToken ct);
}
