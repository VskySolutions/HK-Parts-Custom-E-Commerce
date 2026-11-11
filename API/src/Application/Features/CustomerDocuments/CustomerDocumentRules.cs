using System.IO.Compression;
using System.Text;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerDocuments;

/// <summary>
/// Display labels for <see cref="CustomerDocumentType"/> — the single source the admin dropdown is built from.
/// The list order is the dropdown order.
/// </summary>
public static class CustomerDocumentTypes
{
    public static readonly IReadOnlyList<(CustomerDocumentType Value, string Label)> All = new[]
    {
        (CustomerDocumentType.GovernmentIssuedPhotoId, "Government-Issued Photo ID"),
        (CustomerDocumentType.DriversLicense, "Drivers License"),
        (CustomerDocumentType.LawEnforcementId, "Law Enforcement ID"),
        (CustomerDocumentType.MilitaryId, "Military ID"),
        (CustomerDocumentType.FirearmLicensePermit, "Firearm License / Permit"),
        (CustomerDocumentType.FirearmTrainingCertificate, "Firearm Training Certificate"),
        (CustomerDocumentType.LicenseCopy, "License Copy"),
        (CustomerDocumentType.AddressProof, "Address Proof"),
        (CustomerDocumentType.AdditionalSupportingDocuments, "Additional Supporting Documents"),
    };

    public static string Label(CustomerDocumentType type)
        => All.FirstOrDefault(t => t.Value == type).Label ?? type.ToString();
}

/// <summary>A file as received by the API (buffered, like the other upload endpoints).</summary>
public sealed record DocumentFileInput(byte[] Content, string FileName);

/// <summary>
/// File rules for customer documents: PDF, DOC, DOCX, JPG, PNG up to 10 MB. The type is decided from the
/// <b>content</b> (magic bytes, plus a DOCX package check), never from the browser's Content-Type header, and
/// must agree with the file extension. File names are sanitized before they are stored or echoed.
/// </summary>
public static class CustomerDocumentFiles
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    public const int MaxFilesPerUpload = 10;
    public const string AllowedText = "Only PDF, DOC, DOCX, JPG, PNG files are allowed.";

    /// <summary>Extension → (canonical extension, MIME type).</summary>
    private static readonly IReadOnlyDictionary<string, (string Ext, string Mime)> ByExtension =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = (".pdf", "application/pdf"),
            [".doc"] = (".doc", "application/msword"),
            [".docx"] = (".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            [".jpg"] = (".jpg", "image/jpeg"),
            [".jpeg"] = (".jpg", "image/jpeg"),
            [".png"] = (".png", "image/png"),
        };

    /// <summary>PDFs and images open in the browser; Word documents download.</summary>
    public static bool IsInline(string mimeType) => mimeType is "application/pdf" or "image/jpeg" or "image/png";

    /// <summary>
    /// Validates one file. Returns null and the detected (ext, mime) when it is acceptable, otherwise an error message
    /// that never contains file content.
    /// </summary>
    public static string? Validate(DocumentFileInput? file, out string extension, out string mimeType)
    {
        extension = mimeType = string.Empty;
        if (file is null || file.Content is null || file.Content.Length == 0)
            return "File is required.";
        if (file.Content.LongLength > MaxSizeBytes)
            return $"File exceeds the maximum size of {MaxSizeBytes / 1_048_576} MB.";

        if (!ByExtension.TryGetValue(Path.GetExtension(SanitizeFileName(file.FileName)), out var declared))
            return AllowedText;

        var detected = Detect(file.Content);
        if (detected is null || detected != declared.Mime)
            return "The file content does not match its extension, or is not a PDF, DOC, DOCX, JPG or PNG file.";

        extension = declared.Ext;
        mimeType = declared.Mime;
        return null;
    }

    /// <summary>MIME type from the file's signature, or null when it is none of the allowed formats.</summary>
    public static string? Detect(byte[] b)
    {
        if (StartsWith(b, "%PDF-"u8)) return "application/pdf";
        if (StartsWith(b, [0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (StartsWith(b, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (StartsWith(b, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1])) return "application/msword"; // OLE2 (.doc)
        if (StartsWith(b, [0x50, 0x4B, 0x03, 0x04]) && IsWordPackage(b))
            return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        return null;
    }

    /// <summary>A ZIP is only a DOCX if it is an OOXML package containing the Word main part.</summary>
    private static bool IsWordPackage(byte[] b)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(b, writable: false), ZipArchiveMode.Read);
            return zip.GetEntry("[Content_Types].xml") is not null && zip.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool StartsWith(byte[] data, ReadOnlySpan<byte> prefix)
        => data.Length >= prefix.Length && data.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    /// <summary>
    /// Display-safe original name: drops any path, control characters and characters invalid in file names,
    /// collapses whitespace, and caps the length at 255 while keeping the extension.
    /// </summary>
    public static string SanitizeFileName(string? name)
    {
        var raw = Path.GetFileName((name ?? string.Empty).Replace('\\', '/').Split('/').Last());
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '"', '<', '>', '|', ':', '*', '?' };
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw.Normalize(NormalizationForm.FormC))
            sb.Append(char.IsControl(ch) || invalid.Contains(ch) ? '_' : ch);

        var collapsed = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        // Split first so a name like ".pdf" keeps its extension; trim stray dots/spaces from the stem only.
        var ext = Path.GetExtension(collapsed).Trim();
        var stem = collapsed[..^ext.Length].Trim().Trim('.').Trim();
        if (stem.Length == 0)
            stem = "document";
        if (ext.Length > 20)
            ext = string.Empty; // not a real extension; validation will reject it
        if (stem.Length + ext.Length > 255)
            stem = stem[..(255 - ext.Length)];
        return stem + ext;
    }
}
