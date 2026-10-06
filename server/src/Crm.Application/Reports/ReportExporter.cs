using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using Crm.Application.Common.Exceptions;

namespace Crm.Application.Reports;

/// <summary>A report as a flat table: cells are text, numbers, dates or null (empty).</summary>
public sealed record ReportTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

/// <summary>An exported file ready to send.</summary>
public sealed record ReportFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Writes a <see cref="ReportTable"/> as CSV (UTF-8 with BOM, so Excel reads Arabic text) or as a real Excel file
/// (<c>.xlsx</c>, written as OOXML with <see cref="ZipArchive"/>; numbers stay numeric cells).
/// </summary>
public static class ReportExporter
{
    public const string CsvContentType = "text/csv; charset=utf-8";
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>"csv" or "xlsx" (any case); anything else throws a <see cref="ValidationException"/> on <c>format</c>.</summary>
    public static ReportFile Export(ReportTable table, string? format, string baseFileName) =>
        (format?.Trim().ToLowerInvariant()) switch
        {
            "csv" => new ReportFile(Csv(table), CsvContentType, baseFileName + ".csv"),
            "xlsx" => new ReportFile(Xlsx(table), XlsxContentType, baseFileName + ".xlsx"),
            _ => throw new ValidationException(new Dictionary<string, string[]> { ["format"] = [ReportText.FormatInvalid] }),
        };

    private static byte[] Csv(ReportTable table)
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', table.Columns.Select(CsvCell))).Append("\r\n");
        foreach (var row in table.Rows)
        {
            text.Append(string.Join(',', row.Select(value => CsvCell(Format(value))))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string CsvCell(string text) =>
        text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime time => time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static bool IsNumber(object? value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static byte[] Xlsx(ReportTable table)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Add(zip, "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Add(zip, "xl/workbook.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{SecurityElement.Escape(SheetName(table.Title))}" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
                """);
            Add(zip, "xl/worksheets/sheet1.xml", Sheet(table));
        }

        return stream.ToArray();
    }

    private static string Sheet(ReportTable table)
    {
        var xml = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        AppendRow(xml, 1, table.Columns.Cast<object?>().ToList());
        for (var i = 0; i < table.Rows.Count; i++)
        {
            AppendRow(xml, i + 2, table.Rows[i]);
        }

        return xml.Append("</sheetData></worksheet>").ToString();
    }

    private static void AppendRow(StringBuilder xml, int rowNumber, IReadOnlyList<object?> cells)
    {
        xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowNumber}\">");
        for (var column = 0; column < cells.Count; column++)
        {
            var value = cells[column];
            if (value is null)
            {
                continue; // an empty value writes no cell
            }

            var reference = ColumnName(column) + rowNumber.ToString(CultureInfo.InvariantCulture);
            if (IsNumber(value))
            {
                xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\"><v>{Format(value)}</v></c>");
                continue;
            }

            var text = Clean(Format(value));
            var preserve = text != text.Trim() || text.Contains('\n') ? " xml:space=\"preserve\"" : string.Empty;
            xml.Append(CultureInfo.InvariantCulture,
                $"<c r=\"{reference}\" t=\"inlineStr\"><is><t{preserve}>{SecurityElement.Escape(text)}</t></is></c>");
        }

        xml.Append("</row>");
    }

    /// <summary>Drops the control characters XML 1.0 cannot hold (everything below space except tab, line feed, carriage return).</summary>
    private static string Clean(string text) =>
        new([.. text.Where(c => c >= ' ' || c is '\t' or '\n' or '\r')]);

    /// <summary>0 → "A", 25 → "Z", 26 → "AA".</summary>
    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (var n = index; n >= 0; n = n / 26 - 1)
        {
            name = (char)('A' + n % 26) + name;
        }

        return name;
    }

    // Sheet names: at most 31 characters, none of : \ / ? * [ ]
    private static string SheetName(string title)
    {
        var cleaned = new string([.. title.Where(c => !":\\/?*[]".Contains(c))]).Trim();
        return cleaned.Length == 0 ? "Report" : cleaned[..Math.Min(31, cleaned.Length)];
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.TrimStart());
    }
}
