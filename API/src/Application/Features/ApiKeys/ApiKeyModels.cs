using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ApiKeys;

/// <summary>Masked view of an API key for listings — never exposes the plaintext or the stored hash.</summary>
public class ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Non-secret prefix followed by an ellipsis, e.g. <c>vsk_Ab12cd34…</c>.</summary>
    public string MaskedKey { get; set; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; set; } = new List<string>();
    public bool IsActive { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static ApiKeyDto From(ApiKey k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        MaskedKey = $"{k.Prefix}…",
        Scopes = k.Scopes.ToList(),
        IsActive = k.IsActive,
        ExpiresAtUtc = k.ExpiresAtUtc,
        CreatedOnUtc = k.CreatedOnUtc,
    };
}

/// <summary>Result of creating a key — includes the plaintext exactly once; it can never be retrieved again.</summary>
public class CreatedApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>The plaintext API key. Shown only here, at creation time; store it securely.</summary>
    public string PlainTextKey { get; set; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; set; } = new List<string>();
    public DateTime? ExpiresAtUtc { get; set; }
}
