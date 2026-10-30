using System.Text;

namespace HKPartsEcommerce.Application.Common.Utilities;

/// <summary>Minimal RFC-4180 CSV parser/writer (quoted fields, embedded commas/quotes/newlines).</summary>
public static class Csv
{
    /// <summary>Parses CSV text into records (each a list of field values). Empty trailing lines are dropped.</summary>
    public static List<List<string>> Parse(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        // Strip a UTF-8 BOM if present.
        if (text.Length > 0 && text[0] == '﻿')
            i = 1;

        var sawAny = false;
        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                    inQuotes = false; i++; continue;
                }
                field.Append(c); i++; continue;
            }

            switch (c)
            {
                case '"': inQuotes = true; sawAny = true; i++; break;
                case ',': record.Add(field.ToString()); field.Clear(); sawAny = true; i++; break;
                case '\r': i++; break;
                case '\n':
                    record.Add(field.ToString());
                    records.Add(record);
                    record = new List<string>();
                    field.Clear();
                    sawAny = false;
                    i++;
                    break;
                default: field.Append(c); sawAny = true; i++; break;
            }
        }

        if (sawAny || field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        // Drop rows that are entirely empty.
        return records.Where(r => r.Any(f => !string.IsNullOrWhiteSpace(f))).ToList();
    }

    /// <summary>Writes rows (header first) to CSV text.</summary>
    public static string Write(IEnumerable<string> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", header.Select(Escape))).Append('\n');
        foreach (var row in rows)
            sb.Append(string.Join(",", row.Select(Escape))).Append('\n');
        return sb.ToString();
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
