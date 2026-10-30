using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Utilities;

/// <summary>
/// Pure helpers for the media pipeline (WO-122): MIME → category, SEO file-name generation, and
/// dependency-free image-dimension extraction from file headers.
/// </summary>
public static class MediaHelpers
{
    /// <summary>Classifies a MIME type into a broad <see cref="MediaType"/> category.</summary>
    public static MediaType ClassifyMediaType(string? mimeType)
    {
        var m = (mimeType ?? string.Empty).ToLowerInvariant();
        if (m.StartsWith("image/")) return MediaType.Image;
        if (m.StartsWith("video/")) return MediaType.Video;
        if (m.StartsWith("audio/")) return MediaType.Audio;
        if (m.StartsWith("application/pdf")
            || m.StartsWith("application/msword")
            || m.StartsWith("application/vnd")
            || m.StartsWith("text/")) return MediaType.Document;
        return MediaType.Other;
    }

    /// <summary>
    /// Builds a URL-safe SEO file name from an original file name: strips the extension, lowercases, and
    /// collapses everything that isn't a letter/digit into single hyphens (e.g.
    /// "Product Image Blue_Shirt (1).jpg" → "product-image-blue-shirt-1").
    /// </summary>
    public static string ToSeoFileName(string? originalFileName)
    {
        var name = originalFileName ?? string.Empty;
        var dot = name.LastIndexOf('.');
        if (dot > 0) name = name[..dot];

        name = name.ToLowerInvariant();
        name = Regex.Replace(name, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(name) ? "file" : name;
    }

    /// <summary>True when a string is a valid SEO file name (lowercase letters, digits, single hyphens).</summary>
    public static bool IsValidSeoFileName(string? value)
        => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "^[a-z0-9]+(-[a-z0-9]+)*$");

    /// <summary>The conventional file extension for a MIME type (leading dot), or empty when unknown.</summary>
    public static string ExtensionForMime(string? mimeType) => (mimeType ?? string.Empty).ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "image/svg+xml" => ".svg",
        "image/bmp" => ".bmp",
        "image/avif" => ".avif",
        "application/pdf" => ".pdf",
        "video/mp4" => ".mp4",
        "audio/mpeg" => ".mp3",
        _ => string.Empty,
    };

    /// <summary>
    /// Best-effort pixel-dimension read for common raster formats (PNG, GIF, JPEG, BMP, WEBP) straight from
    /// the file header — no image library. Returns false (and null-equivalent zeros) when it can't parse.
    /// </summary>
    public static bool TryReadImageDimensions(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (data.Length < 24) return false;

        // PNG: 8-byte signature, then IHDR (width @16, height @20, big-endian).
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(16, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(20, 4));
            return width > 0 && height > 0;
        }

        // GIF: "GIF87a"/"GIF89a", width @6, height @8 (little-endian).
        if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F')
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));
            height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(8, 2));
            return width > 0 && height > 0;
        }

        // BMP: 'BM', width @18, height @22 (little-endian).
        if (data[0] == 'B' && data[1] == 'M')
        {
            width = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(18, 4));
            height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(22, 4)));
            return width > 0 && height > 0;
        }

        // JPEG: scan Start-Of-Frame markers for the frame dimensions.
        if (data[0] == 0xFF && data[1] == 0xD8)
            return TryReadJpeg(data, out width, out height);

        // WEBP: RIFF....WEBP then a VP8/VP8L/VP8X chunk.
        if (data.Length >= 30 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
            && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
            return TryReadWebp(data, out width, out height);

        return false;
    }

    private static bool TryReadJpeg(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        var i = 2;
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF) { i++; continue; }
            var marker = data[i + 1];
            // SOF markers carry the frame size (skip DHT/DAC/RSTn and standalone markers).
            var isSof = marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            var segLen = (data[i + 2] << 8) | data[i + 3];
            if (isSof)
            {
                height = (data[i + 5] << 8) | data[i + 6];
                width = (data[i + 7] << 8) | data[i + 8];
                return width > 0 && height > 0;
            }
            if (segLen < 2) return false;
            i += 2 + segLen;
        }
        return false;
    }

    private static bool TryReadWebp(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        var fourcc = Encoding.ASCII.GetString(data.Slice(12, 4));
        switch (fourcc)
        {
            case "VP8X": // extended: 24-bit canvas width-1/height-1 at offset 24.
                width = (data[24] | (data[25] << 8) | (data[26] << 16)) + 1;
                height = (data[27] | (data[28] << 8) | (data[29] << 16)) + 1;
                return true;
            case "VP8 ": // lossy: 14-bit dimensions after the 3-byte start code at offset 26.
                width = ((data[27] << 8) | data[26]) & 0x3FFF;
                height = ((data[29] << 8) | data[28]) & 0x3FFF;
                return width > 0 && height > 0;
            case "VP8L": // lossless: 14-bit dimensions packed from offset 21.
                var bits = data[21] | (data[22] << 8) | (data[23] << 16) | (data[24] << 24);
                width = (bits & 0x3FFF) + 1;
                height = ((bits >> 14) & 0x3FFF) + 1;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Parses a byte count from a settings string, falling back to <paramref name="fallback"/>.</summary>
    public static long ParseBytes(string? raw, long fallback)
        => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : fallback;
}
