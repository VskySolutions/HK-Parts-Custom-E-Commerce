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
}
