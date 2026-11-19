using System.Text.RegularExpressions;

namespace HKPartsEcommerce.Application.Features.Shipping;

/// <summary>
/// The single P.O. box rule (docs/shipstation-verification-requirements.md AV-32), shared by the checkout
/// destination rules, address verification and the admin. Catches "PO Box", "P.O. Box", "P O Box",
/// "Post Office Box", "Postal Box", "Lock Box", "POB 12", "P.O.B. 12", "P Box 12", "POBox12", and "Box 12"
/// when it is the whole line. Does not catch street names such as "Box Elder St", "Boxwood Ln" or "1 Post Rd".
/// </summary>
public static class PoBoxDetector
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Regex[] Patterns =
    {
        // Post Office Box / Postal Box / Lock Box
        new(@"\b(?:post\s*office|postal|lock)\s*box(?![a-z])", Options),
        // PO Box, P.O. Box, P. O. Box, P O Box, POBox12
        new(@"\bp\s*\.?\s*o\s*\.?\s*box(?![a-z])", Options),
        // POB 12, P.O.B. 12, POB #12 — only with a number, so a "Pob…" street name is not caught
        new(@"\bp\s*\.?\s*o\s*\.?\s*b\s*\.?\s*#?\s*\d", Options),
        // P Box 12
        new(@"\bp\s*\.?\s*box\s*#?\s*\d", Options),
        // "Box 12" / "Bx 12" as the whole line
        new(@"^\s*(?:box|bx)\s*\.?\s*#?\s*\d+[a-z]?\s*$", Options),
    };

    public static bool IsPoBox(string? line) =>
        !string.IsNullOrWhiteSpace(line) && Patterns.Any(p => p.IsMatch(line));

    /// <summary>True when any of the address lines is a P.O. box.</summary>
    public static bool IsPoBox(string? line1, string? line2) => IsPoBox(line1) || IsPoBox(line2);
}
