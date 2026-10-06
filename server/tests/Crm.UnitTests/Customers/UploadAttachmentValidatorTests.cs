using Crm.Application.Common.Localization;
using Crm.Application.Customers.Attachments;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Customers;

/// <summary>CRM-11 AC 3 / AC 4: size limit and allowed types of an upload.</summary>
public class UploadAttachmentValidatorTests
{
    private readonly UploadAttachmentRequestValidator _validator = new();

    private static UploadAttachmentRequest Upload(string? fileName, long length) =>
        new(fileName, length, fileName is null ? null : Stream.Null);

    [Theory]
    [InlineData("report.pdf", 1)]
    [InlineData("report.pdf", AttachmentRules.MaxSizeBytes)]
    public void AllowedFile_UpTo10MB_HasNoErrors(string fileName, long length) =>
        Assert.True(_validator.Validate(Upload(fileName, length)).IsValid);

    [Fact]
    public void FileOver10MB_ReportsFile()
    {
        var result = _validator.Validate(Upload("report.pdf", AttachmentRules.MaxSizeBytes + 1));

        var error = Assert.Single(result.Errors);
        Assert.Equal("File", error.PropertyName);
        Assert.Equal("The file is larger than 10 MB.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("invoice.pdf.exe")]
    public void DisallowedType_ReportsFile(string fileName)
    {
        var result = _validator.Validate(Upload(fileName, 100));

        Assert.Equal(["File"], result.Errors.Select(e => e.PropertyName).Distinct());
        Assert.StartsWith("This file type is not allowed.", result.Errors[0].ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("report.pdf", 0)]
    public void MissingOrEmptyFile_ReportsFile(string? fileName, long length) =>
        Assert.Equal(["File"], _validator.Validate(Upload(fileName, length)).Errors.Select(e => e.PropertyName).Distinct());

    [Fact]
    public void FileOver10MB_InArabic_HasTheArabicMessage()
    {
        var result = UiCulture.Use(LocalizedText.Arabic,
            () => _validator.Validate(Upload("report.pdf", AttachmentRules.MaxSizeBytes + 1)));

        Assert.Equal("حجم الملف أكبر من 10 ميجابايت.", Assert.Single(result.Errors).ErrorMessage);
    }
}
