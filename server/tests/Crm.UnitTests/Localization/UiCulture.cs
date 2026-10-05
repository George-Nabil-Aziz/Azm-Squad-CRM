using System.Globalization;

namespace Crm.UnitTests.Localization;

/// <summary>Runs code with a given UI culture and restores the previous one (tests must not leak culture).</summary>
internal static class UiCulture
{
    public static T Use<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
