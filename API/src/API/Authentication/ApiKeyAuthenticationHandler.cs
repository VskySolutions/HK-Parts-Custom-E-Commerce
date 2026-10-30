using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.API.Authentication;

/// <summary>Scheme names, header, and claim types for API-key (machine-to-machine) authentication.</summary>
public static class ApiKeyDefaults
{
    /// <summary>Authentication scheme name for API-key callers.</summary>
    public const string Scheme = "ApiKey";

    /// <summary>Composite policy scheme that forwards to JWT bearer or <see cref="Scheme"/> per request.</summary>
    public const string PolicyScheme = "JwtOrApiKey";

    /// <summary>Header carrying the plaintext API key.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>Claim holding the authenticated key's id.</summary>
    public const string ApiKeyIdClaim = "apiKeyId";

    /// <summary>Claim (repeated) holding each module the key is scoped to.</summary>
    public const string ScopeClaim = "scope";
}

/// <summary>
/// Authenticates machine-to-machine callers presenting the <c>X-Api-Key</c> header. On success the
/// principal carries the key id and one <c>scope</c> claim per authorized module — and no role, so
/// access is governed by scopes alone.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiKeyService _apiKeys;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeys)
        : base(options, logger, encoder)
    {
        _apiKeys = apiKeys;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var presented = values.ToString();
        if (string.IsNullOrWhiteSpace(presented))
            return AuthenticateResult.NoResult();

        var identity = await _apiKeys.AuthenticateAsync(presented, Context.RequestAborted);
        if (identity is null)
            return AuthenticateResult.Fail("Invalid, revoked, or expired API key.");

        var claims = new List<Claim>
        {
            new(ApiKeyDefaults.ApiKeyIdClaim, identity.ApiKeyId.ToString()),
            new("name", identity.Name),
        };
        claims.AddRange(identity.Scopes.Select(s => new Claim(ApiKeyDefaults.ScopeClaim, s)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
