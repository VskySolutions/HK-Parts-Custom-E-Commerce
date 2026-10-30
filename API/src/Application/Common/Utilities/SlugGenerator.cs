using System.Text.RegularExpressions;

namespace HKPartsEcommerce.Application.Common.Utilities;

/// <summary>
/// Generates URL-safe slugs (lowercase, hyphen-separated, alphanumeric only) from arbitrary text.
/// Used to derive CMS page/blog/group slugs from a title/name when the author leaves the slug blank,
/// and to normalise any slug the author does supply (WO-54).
/// </summary>
public static class SlugGenerator
{
    /// <summary>
    /// Lowercases <paramref name="value"/>, collapses every run of non-alphanumeric characters into a
    /// single hyphen and trims leading/trailing hyphens (e.g. "About Us &amp; Contact!" → "about-us-contact").
    /// Falls back to a short random token when the input has no alphanumeric characters, so the result is
    /// always a non-empty, index-safe slug.
    /// </summary>
    public static string Generate(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        text = Regex.Replace(text, "[^a-z0-9]+", "-").Trim('-');
        return text.Length == 0 ? Guid.NewGuid().ToString("n")[..8] : text;
    }
}
