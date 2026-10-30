using System.Text;
using System.Text.Json;

namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// One value a buyer typed into a product's <c>CustomInput</c> attribute (e.g. "Engraving": "For Ana"),
/// snapshotted onto the cart line and carried through to the order line. The attribute's
/// <see cref="Name"/> is stored alongside its id so a later rename or deletion cannot change what a
/// placed order says was ordered.
/// </summary>
public class CustomAttributeSelection
{
    public Guid AttributeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Read/write helpers for the <c>CustomAttributesJson</c> column on cart and order lines. The values
/// live as a JSON array rather than a child table because they are an immutable snapshot: nothing
/// queries them relationally, and the serialized form doubles as the line's identity for cart merging
/// (two "Engraving" lines with different text must stay separate lines).
/// </summary>
public static class CustomAttributes
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Reads a stored payload; a null, empty or malformed one yields no values rather than throwing.</summary>
    public static List<CustomAttributeSelection> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new();

        try
        {
            return JsonSerializer.Deserialize<List<CustomAttributeSelection>>(json, Options) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>Writes the payload for storage, ordered by attribute id so the same set always serializes identically. Empty → null.</summary>
    public static string? Serialize(IEnumerable<CustomAttributeSelection>? values)
    {
        var ordered = (values ?? Enumerable.Empty<CustomAttributeSelection>())
            .OrderBy(v => v.AttributeId)
            .ToList();

        return ordered.Count == 0 ? null : JsonSerializer.Serialize(ordered, Options);
    }

    /// <summary>
    /// A stable identity for a line's custom values, used to decide whether an incoming item merges into
    /// an existing cart line. Derived from the parsed values (not the raw string) so formatting
    /// differences never split a line that is really the same.
    /// </summary>
    public static string Signature(string? json) =>
        string.Join("", Parse(json)
            .OrderBy(v => v.AttributeId)
            .Select(v => $"{v.AttributeId:n}={v.Value}"));

    /// <summary>A one-line "Name: Value" rendering for documents that have no structured layout (invoice, packing slip).</summary>
    public static string Describe(string? json)
    {
        var builder = new StringBuilder();
        foreach (var value in Parse(json))
        {
            if (builder.Length > 0)
                builder.Append(" · ");
            builder.Append(value.Name).Append(": ").Append(value.Value);
        }
        return builder.ToString();
    }
}
