using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>
/// Provides the deployment's RS256 signing key. Loads a PEM private key from the configured path, or
/// generates and persists one on first run so tokens remain valid across restarts.
/// </summary>
public sealed class RsaKeyProvider : IDisposable
{
    private readonly RSA _rsa;

    public RsaSecurityKey SecurityKey { get; }
    public SigningCredentials SigningCredentials { get; }

    public RsaKeyProvider(IConfiguration configuration, ILogger<RsaKeyProvider> logger)
    {
        _rsa = RSA.Create(2048);

        var keyPath = configuration["Jwt:PrivateKeyPath"];
        if (string.IsNullOrWhiteSpace(keyPath))
            keyPath = "keys/jwt-private.pem";

        if (File.Exists(keyPath))
        {
            _rsa.ImportFromPem(File.ReadAllText(keyPath));
            logger.LogInformation("Loaded RSA signing key from {Path}.", keyPath);
        }
        else
        {
            var dir = Path.GetDirectoryName(keyPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(keyPath, _rsa.ExportRSAPrivateKeyPem());
            logger.LogWarning(
                "No RSA signing key found; generated a new one at {Path}. Provision a managed key for production.",
                keyPath);
        }

        SecurityKey = new RsaSecurityKey(_rsa) { KeyId = "vsky-rsa-1" };
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);
    }

    public void Dispose() => _rsa.Dispose();
}
