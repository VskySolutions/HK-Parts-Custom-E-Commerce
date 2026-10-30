using Microsoft.AspNetCore.Identity;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>PBKDF2 password hashing via ASP.NET Core Identity's hasher.</summary>
public class PasswordHasher : IPasswordHasher
{
    private static readonly object Marker = new();
    private readonly PasswordHasher<object> _inner = new();

    public string Hash(string password) => _inner.HashPassword(Marker, password);

    public bool Verify(string hashedPassword, string providedPassword) =>
        _inner.VerifyHashedPassword(Marker, hashedPassword, providedPassword) != PasswordVerificationResult.Failed;
}
