using System.Reflection;
using System.Text.RegularExpressions;
using Crm.Application.Auth;
using Crm.Application.Common.Localization;
using Crm.Application.Users;

namespace Crm.UnitTests.Localization;

/// <summary>
/// Guard: every user-facing text class in the Application layer (a static class named "*Text") returns
/// English text for "en" and Arabic text for "ar". Text classes added by later stories are picked up automatically.
/// </summary>
public partial class LocalizedTextCatalogTests
{
    [GeneratedRegex(@"[؀-ۿ]")]
    private static partial Regex ArabicLetter();

    private static readonly Type[] TextClasses = typeof(Crm.Application.AssemblyReference).Assembly.GetTypes()
        .Where(type => type is { IsAbstract: true, IsSealed: true } // static class
                       && type.Name.EndsWith("Text", StringComparison.Ordinal)
                       && type != typeof(LocalizedText))
        .ToArray();

    public static TheoryData<string> TextProperties()
    {
        var data = new TheoryData<string>();
        foreach (var property in TextClasses
                     .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Static))
                     .Where(property => property.PropertyType == typeof(string)))
        {
            data.Add($"{property.DeclaringType!.Name}.{property.Name}");
        }

        return data;
    }

    [Fact]
    public void Catalog_FindsTheTextClasses()
    {
        Assert.Contains(typeof(AuthText), TextClasses);
        Assert.Contains(typeof(ErrorText), TextClasses);
        Assert.Contains(typeof(UserText), TextClasses);
    }

    [Theory]
    [MemberData(nameof(TextProperties))]
    public void TextProperty_HasEnglishAndArabicText(string name)
    {
        var parts = name.Split('.');
        var property = TextClasses.Single(type => type.Name == parts[0])
            .GetProperty(parts[1], BindingFlags.Public | BindingFlags.Static)!;

        var english = UiCulture.Use("en", () => (string)property.GetValue(null)!);
        var arabic = UiCulture.Use("ar", () => (string)property.GetValue(null)!);

        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.DoesNotMatch(ArabicLetter(), english);
        Assert.Matches(ArabicLetter(), arabic);
    }
}
