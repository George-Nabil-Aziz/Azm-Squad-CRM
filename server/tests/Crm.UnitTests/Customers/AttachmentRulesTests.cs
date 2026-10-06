using Crm.Application.Customers.Attachments;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-11: which files may be attached (allow-list by extension) and how their names are kept.</summary>
public class AttachmentRulesTests
{
    [Theory]
    [InlineData("report.pdf", "application/pdf")]
    [InlineData("photo.PNG", "image/png")]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("anim.gif", "image/gif")]
    [InlineData("pic.webp", "image/webp")]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("data.csv", "text/csv")]
    [InlineData("old.doc", "application/msword")]
    [InlineData("contract.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("sheet.xls", "application/vnd.ms-excel")]
    [InlineData("sheet.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public void TryGetContentType_AllowedType_ReturnsTheServerContentType(string fileName, string expected)
    {
        Assert.True(AttachmentRules.TryGetContentType(fileName, out var contentType));
        Assert.Equal(expected, contentType);
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("SETUP.EXE")]
    [InlineData("run.bat")]
    [InlineData("script.js")]
    [InlineData("invoice.pdf.exe")]
    [InlineData("README")]
    [InlineData("")]
    public void TryGetContentType_DisallowedType_ReturnsFalse(string fileName) =>
        Assert.False(AttachmentRules.TryGetContentType(fileName, out _));

    [Theory]
    [InlineData(@"C:\temp\report.pdf", "report.pdf")]
    [InlineData("../../etc/report.pdf", "report.pdf")]
    [InlineData("  تقرير.pdf  ", "تقرير.pdf")]
    [InlineData("bad\u0001name.pdf", "badname.pdf")]
    public void SafeFileName_KeepsOnlyTheName(string fileName, string expected) =>
        Assert.Equal(expected, AttachmentRules.SafeFileName(fileName));

    [Fact]
    public void MaxSize_Is10MB() => Assert.Equal(10 * 1024 * 1024, AttachmentRules.MaxSizeBytes);
}
