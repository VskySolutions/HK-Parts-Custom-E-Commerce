namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>JWT configuration bound from the "Jwt" configuration section.</summary>
public class JwtOptions
{
    public string Issuer { get; set; } = "HKPartsEcommerce";
    public string Audience { get; set; } = "HKPartsEcommerce.Client";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
    public string PrivateKeyPath { get; set; } = "keys/jwt-private.pem";
}
