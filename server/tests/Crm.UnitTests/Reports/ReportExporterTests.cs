using System.IO.Compression;
using System.Text;
using Crm.Application.Common.Exceptions;
using Crm.Application.Reports;

namespace Crm.UnitTests.Reports;

public class ReportExporterTests
{
    private static readonly ReportTable Table = new(
        "Tickets",
        ["Report", "Value", "Count"],
        [
            ["Status", "new", 5],
            ["Category", "Billing, \"VIP\"", 2],
            ["Category", "فواتير", 0],
            ["Day", "line1\nline2", null],
        ]);

    [Fact]
    public void Csv_StartsWithAUtf8Bom_AndEscapesCommasQuotesAndNewlines()
    {
        var file = ReportExporter.Export(Table, "csv", "report");

        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal("report.csv", file.FileName);
        Assert.Equal([0xEF, 0xBB, 0xBF], file.Content.Take(3));
        var text = Encoding.UTF8.GetString(file.Content, 3, file.Content.Length - 3);
        Assert.Equal(
            "Report,Value,Count\r\nStatus,new,5\r\nCategory,\"Billing, \"\"VIP\"\"\",2\r\nCategory,فواتير,0\r\nDay,\"line1\nline2\",\r\n",
            text);
    }

    [Fact]
    public void Csv_FormatsNumbersAndDatesWithTheInvariantCulture()
    {
        var table = new ReportTable("T", ["A", "B", "C"], [[1.5, new DateOnly(2026, 10, 6), new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc)]]);

        var text = Encoding.UTF8.GetString(ReportExporter.Export(table, "CSV", "r").Content).TrimStart('﻿');

        Assert.Equal("A,B,C\r\n1.5,2026-10-06,2026-10-06T08:00:00Z\r\n", text);
    }

    [Fact]
    public void Xlsx_IsAZipWithAWorksheet_HoldingTextAndNumericCells()
    {
        var file = ReportExporter.Export(Table, "xlsx", "report");

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Equal("report.xlsx", file.FileName);
        using var zip = new ZipArchive(new MemoryStream(file.Content));
        Assert.Equal(
            ["[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels", "xl/workbook.xml", "xl/worksheets/sheet1.xml"],
            zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open(), Encoding.UTF8);
        var sheet = reader.ReadToEnd();
        Assert.Contains("<t>Report</t>", sheet);
        Assert.Contains("<t>Billing, &quot;VIP&quot;</t>", sheet);
        Assert.Contains("<t>فواتير</t>", sheet);
        Assert.Contains("<c r=\"C2\"><v>5</v></c>", sheet); // a number, not text
        Assert.Contains("<c r=\"C3\"><v>2</v></c>", sheet);
        Assert.DoesNotContain("r=\"C5\"", sheet); // an empty value writes no cell
    }

    [Fact]
    public void Xlsx_RemovesCharactersXmlCannotHold()
    {
        var table = new ReportTable("T", ["A"], [["bad\u0001char"]]);

        using var zip = new ZipArchive(new MemoryStream(ReportExporter.Export(table, "xlsx", "r").Content));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open(), Encoding.UTF8);

        Assert.Contains("<t>badchar</t>", reader.ReadToEnd());
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("")]
    [InlineData(null)]
    public void AnUnknownFormat_IsAValidationError_OnFormat(string? format)
    {
        var error = Assert.Throws<ValidationException>(() => ReportExporter.Export(Table, format, "report"));

        Assert.Contains("format", error.Errors.Keys);
    }
}
