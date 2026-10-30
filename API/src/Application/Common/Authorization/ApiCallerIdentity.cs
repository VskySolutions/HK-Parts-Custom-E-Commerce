namespace HKPartsEcommerce.Application.Common.Authorization;

/// <summary>
/// The identity of an authenticated machine-to-machine caller. Carries no user context — access is
/// governed by <see cref="Scopes"/> (module names) alone, with no role assigned.
/// </summary>
public sealed record ApiCallerIdentity(Guid ApiKeyId, string Name, IReadOnlyList<string> Scopes);
