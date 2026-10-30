namespace HKPartsEcommerce.Domain.Common;

/// <summary>
/// Marks a string property whose column is encrypted at rest. <see cref="Persistence"/> applies a Data
/// Protection value converter to every property carrying this attribute (see
/// <c>AppDbContext.OnModelCreating</c>), so the CLR property is always plaintext while the stored column
/// holds ciphertext. Encrypted columns must be unbounded (no max length) so the ciphertext fits.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class EncryptedAttribute : Attribute
{
}
