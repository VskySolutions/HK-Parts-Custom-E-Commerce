using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A credential for a machine-to-machine (M2M) caller, presented via the <c>X-Api-Key</c> header.
/// Only the SHA-256 hash of the key is persisted — the plaintext is shown once at creation and never
/// stored. Access is governed exclusively by <see cref="Scopes"/> (module names); no role is implied.
/// </summary>
public class ApiKey : AuditableEntity, ISoftDeletable
{
    /// <summary>Human-friendly label for the key (e.g. "Fulfilment integration").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Base64 SHA-256 hash of the plaintext key. The plaintext itself is never persisted.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Non-secret leading segment of the key, retained for masked display in listings.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Modules this key may access. An M2M caller is authorized by these scopes alone.</summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>When false the key is revoked and rejected at authentication time.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional absolute expiry; a key at or past this instant is rejected with 401.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
