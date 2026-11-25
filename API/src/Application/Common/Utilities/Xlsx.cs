using System.IO.Compression;
using System.Security;
using System.Text;

namespace HKPartsEcommerce.Application.Common.Utilities;

/// <summary>
/// A minimal Excel (.xlsx) writer for exports, with no extra package: one worksheet, a bold header row, every cell
/// an inline string (so ZIP codes, phone numbers and order numbers keep their leading zeros) except values given as
/// numbers. Produces a valid Office Open XML workbook that Excel, LibreOffice and Google Sheets open.
/// </summary>
public static class Xlsx
{
    public static byte[] Write(string sheetName, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "</Types>");
            Add(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>");
            Add(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                $"<sheets><sheet name=\"{Escape(SheetName(sheetName))}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>");
            // Style 1 = bold (the header row).
            Add(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>" +
                "</styleSheet>");

            var sheet = new StringBuilder();
            sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
                .Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            AppendRow(sheet, 1, header.Cast<object?>().ToList(), bold: true);
            var r = 2;
            foreach (var row in rows)
                AppendRow(sheet, r++, row, bold: false);
            sheet.Append("</sheetData></worksheet>");
            Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
        }
        return ms.ToArray();
    }

    private static void AppendRow(StringBuilder sb, int rowNumber, IReadOnlyList<object?> cells, bool bold)
    {
        sb.Append("<row r=\"").Append(rowNumber).Append("\">");
        for (var c = 0; c < cells.Count; c++)
        {
            var reference = Column(c) + rowNumber;
            var style = bold ? " s=\"1\"" : string.Empty;
            switch (cells[c])
            {
                case null:
                    break;
                case int or long or decimal or double:
                    sb.Append("<c r=\"").Append(reference).Append('"').Append(style).Append("><v>")
                      .Append(Convert.ToString(cells[c], System.Globalization.CultureInfo.InvariantCulture)).Append("</v></c>");
                    break;
                default:
                    sb.Append("<c r=\"").Append(reference).Append('"').Append(style).Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                      .Append(Escape(cells[c]!.ToString() ?? string.Empty)).Append("</t></is></c>");
                    break;
            }
        }
        sb.Append("</row>");
    }

    private static string Column(int index)
    {
        var name = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
            name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    /// <summary>Excel sheet names: at most 31 characters, none of : \ / ? * [ ].</summary>
    private static string SheetName(string name)
    {
        var cleaned = new string(name.Where(ch => ":\\/?*[]".IndexOf(ch) < 0).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "Sheet1";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    // XML-escape and drop characters XML 1.0 can't carry (control characters other than tab / newline).
    private static string Escape(string value) =>
        SecurityElement.Escape(new string(value.Where(ch => ch is '\t' or '\n' or '\r' || ch >= ' ').ToArray())) ?? string.Empty;

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
