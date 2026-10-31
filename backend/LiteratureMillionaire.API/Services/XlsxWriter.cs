using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Writes a one-sheet .xlsx: a bold, frozen header row and rows of text and numbers. Enough for an export
/// someone opens in Excel to hand out prizes, without a spreadsheet library in the API.
///
/// Text is always written as an inline string, never as a formula, so a participant named "=HYPERLINK(...)"
/// is shown as exactly that and never runs.
/// </summary>
public static class XlsxWriter
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>A column: its header and width in characters.</summary>
    public sealed record Column(string Header, double Width);

    /// <param name="sheetName">At most 31 characters, none of []:*?/\ (Excel's rules).</param>
    /// <param name="rows">Each cell is a string, an int, a double or null (empty).</param>
    public static byte[] Write(string sheetName, IReadOnlyList<Column> columns, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Entry(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                + "</Types>");
            Entry(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");
            Entry(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><sheets>"
                + $"<sheet name=\"{Escape(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/>"
                + "</sheets></workbook>");
            Entry(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
                + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
                + "</Relationships>");
            Entry(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + $"<styleSheet xmlns=\"{Main}\">"
                + "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
                + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
                + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
                + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
                + "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>"
                + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
                + "</styleSheet>");
            Entry(zip, "xl/worksheets/sheet1.xml", Sheet(columns, rows));
        }

        return buffer.ToArray();
    }

    private static string Sheet(IReadOnlyList<Column> columns, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        xml.Append($"<worksheet xmlns=\"{Main}\">");
        // The header stays in view while scrolling a long list.
        xml.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        xml.Append("<cols>");
        for (var i = 0; i < columns.Count; i++)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{columns[i].Width}\" customWidth=\"1\"/>");
        }
        xml.Append("</cols><sheetData>");

        xml.Append("<row r=\"1\">");
        for (var i = 0; i < columns.Count; i++)
        {
            xml.Append($"<c r=\"{Reference(i, 1)}\" t=\"inlineStr\" s=\"1\"><is><t>{Escape(columns[i].Header)}</t></is></c>");
        }
        xml.Append("</row>");

        var r = 1;
        foreach (var row in rows)
        {
            r++;
            xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{r}\">");
            for (var i = 0; i < row.Count; i++)
            {
                switch (row[i])
                {
                    case null:
                        break;
                    case int number:
                        xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{Reference(i, r)}\"><v>{number}</v></c>");
                        break;
                    case double number:
                        xml.Append($"<c r=\"{Reference(i, r)}\"><v>{number.ToString("0.###", CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    default:
                        xml.Append($"<c r=\"{Reference(i, r)}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(Convert.ToString(row[i], CultureInfo.InvariantCulture)!)}</t></is></c>");
                        break;
                }
            }
            xml.Append("</row>");
        }

        xml.Append("</sheetData></worksheet>");
        return xml.ToString();
    }

    /// <summary>"A1", "B7", ..., "AA3".</summary>
    private static string Reference(int columnIndex, int row)
    {
        var name = string.Empty;
        for (var n = columnIndex + 1; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + (n - 1) % 26) + name;
        }
        return name + row.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>XML-escaped, with characters XML cannot hold at all (control characters) dropped.</summary>
    private static string Escape(string value)
    {
        var clean = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (XmlConvert.IsXmlChar(ch) || char.IsSurrogate(ch)) clean.Append(ch);
        }
        return System.Security.SecurityElement.Escape(clean.ToString());
    }

    private static void Entry(ZipArchive zip, string name, string content)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        stream.Write(bytes);
    }
}
