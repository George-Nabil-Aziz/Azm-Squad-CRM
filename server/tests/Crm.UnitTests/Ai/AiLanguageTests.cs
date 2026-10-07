using Crm.Application.Ai;

namespace Crm.UnitTests.Ai;

public class AiLanguageTests
{
    [Fact]
    public void Arabic_WhenMostLettersAreArabic() =>
        Assert.Equal("ar", AiLanguage.Detect(["الفاتورة غير صحيحة، أرجو المساعدة"]));

    [Fact]
    public void English_WhenMostLettersAreLatin() =>
        Assert.Equal("en", AiLanguage.Detect(["The invoice is wrong"]));

    [Fact]
    public void MixedText_FollowsTheMajority() =>
        Assert.Equal("ar", AiLanguage.Detect(["مرحبا لدي مشكلة في login وفي الفاتورة الشهرية"]));

    [Fact]
    public void NoLetters_UsesTheFallback_ThenEnglish()
    {
        Assert.Equal("ar", AiLanguage.Detect(["12345", null, ""], "مشكلة"));
        Assert.Equal("en", AiLanguage.Detect(["12345"]));
    }

    [Fact]
    public void Names_AreEnglishAndArabic()
    {
        Assert.Equal("Arabic", AiLanguage.Name("ar"));
        Assert.Equal("English", AiLanguage.Name("en"));
    }
}
