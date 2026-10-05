using Crm.Application.Common.Localization;

namespace Crm.UnitTests.Localization;

public class LocalizedTextTests
{
    [Theory]
    [InlineData("ar")]
    [InlineData("ar-SA")]
    public void Get_WithArabicUiCulture_ReturnsArabic(string culture)
    {
        var text = UiCulture.Use(culture, () => LocalizedText.Get("Hello", "مرحبا"));

        Assert.Equal("مرحبا", text);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("fr")]
    public void Get_WithAnyOtherUiCulture_ReturnsEnglish(string culture)
    {
        var text = UiCulture.Use(culture, () => LocalizedText.Get("Hello", "مرحبا"));

        Assert.Equal("Hello", text);
    }
}
