using System.Globalization;

namespace Crm.Application.Common.Localization;

/// <summary>
/// Picks the text for the request language. The API sets <see cref="CultureInfo.CurrentUICulture"/> from the
/// Accept-Language header (supported: "en", "ar"; default "en").
/// </summary>
public static class LocalizedText
{
    public const string English = "en";
    public const string Arabic = "ar";

    /// <summary>Every UI language the API supports. The first one is the default.</summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } = [English, Arabic];

    /// <summary>Arabic text when the current UI culture is Arabic ("ar", "ar-SA", …), otherwise English.</summary>
    public static string Get(string english, string arabic) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Arabic ? arabic : english;
}
