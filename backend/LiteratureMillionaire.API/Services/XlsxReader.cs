using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Reads the first sheet of an .xlsx as rows of text - the counterpart of <see cref="XlsxWriter"/>, and enough
/// for the one thing the panel does with a workbook: import questions somebody prepared in Excel.
///
/// Only what a question bank needs is understood: shared and inline strings, numbers and booleans as the text
/// Excel shows. Formulas are read as their last cached result, because that is what the person who saved the
/// file was looking at. Anything else - styles, dates as serial numbers, merged cells - is left alone; a date
/// in a question bank would be a mistake anyway.
/// </summary>
public static class XlsxReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>A workbook that cannot be read at all: not a zip, no sheet, unreadable XML.</summary>
    public sealed class NotAWorkbookException(string message) : Exception(message);

    /// <summary>
    /// Rows of the first sheet, each padded to the widest row so a column index is always safe. Empty trailing
    /// rows are dropped; a row that is entirely blank in the middle is kept, because losing it would shift every
    /// row number in the report away from what the person sees in Excel.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Read(Stream file, int maxRows = 5000)
    {
        using var zip = OpenZip(file);
        var shared = ReadSharedStrings(zip);
        var sheet = zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase))
            ?? throw new NotAWorkbookException("Faylda cədvəl yoxdur.");

        XDocument document;
        using (var stream = sheet.Open())
        {
            try
            {
                document = XDocument.Load(stream);
            }
            catch (Exception ex)
            {
                throw new NotAWorkbookException("Cədvəli oxumaq alınmadı: " + ex.GetType().Name);
            }
        }

        var rows = new List<List<string>>();
        var widest = 0;
        foreach (var row in document.Root?.Element(Main + "sheetData")?.Elements(Main + "row") ?? [])
        {
            if (rows.Count >= maxRows)
            {
                break;
            }

            // A row's own r= is what Excel calls it; gaps mean empty rows, which are kept so numbering matches.
            var number = (int?)row.Attribute("r") ?? rows.Count + 1;
            while (rows.Count < number - 1)
            {
                rows.Add([]);
            }

            var cells = new List<string>();
            foreach (var cell in row.Elements(Main + "c"))
            {
                var index = ColumnIndex((string?)cell.Attribute("r"), cells.Count);
                while (cells.Count < index)
                {
                    cells.Add(string.Empty);
                }
                cells.Add(CellText(cell, shared));
            }

            widest = Math.Max(widest, cells.Count);
            rows.Add(cells);
        }

        while (rows.Count > 0 && rows[^1].All(string.IsNullOrWhiteSpace))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        foreach (var row in rows)
        {
            while (row.Count < widest)
            {
                row.Add(string.Empty);
            }
        }

        return rows;
    }

    private static ZipArchive OpenZip(Stream file)
    {
        try
        {
            return new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            throw new NotAWorkbookException("Bu fayl Excel faylı deyil (.xlsx gözlənilir).");
        }
    }

    private static string[] ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return (document.Root?.Elements(Main + "si") ?? []).Select(Text).ToArray();
    }

    /// <summary>All the text of a string item, with its runs joined - Excel splits a styled cell into runs.</summary>
    private static string Text(XElement element) =>
        string.Concat(element.Descendants(Main + "t").Select(t => t.Value));

    private static string CellText(XElement cell, string[] shared)
    {
        var type = (string?)cell.Attribute("t");
        switch (type)
        {
            case "s":
                var index = int.TryParse(cell.Element(Main + "v")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : -1;
                return index >= 0 && index < shared.Length ? shared[index] : string.Empty;
            case "inlineStr":
                return Text(cell.Element(Main + "is") ?? new XElement("empty"));
            case "b":
                return cell.Element(Main + "v")?.Value == "1" ? "TRUE" : "FALSE";
            case "str": // a formula's cached text
                return cell.Element(Main + "v")?.Value ?? string.Empty;
            default:
                return cell.Element(Main + "v")?.Value ?? string.Empty;
        }
    }

    /// <summary>"C7" → 2. Zero-based; a cell with no reference simply lands next, where it was written.</summary>
    private static int ColumnIndex(string? reference, int fallback)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return fallback;
        }

        var index = 0;
        foreach (var ch in reference)
        {
            if (!char.IsAsciiLetter(ch))
            {
                break;
            }
            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return Math.Max(0, index - 1);
    }
}
