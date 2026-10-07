using Crm.Application.Common.Localization;

namespace Crm.Application.Branding;

/// <summary>User-facing text of the branding feature, in the request language.</summary>
public static class BrandingText
{
    public static string ColorInvalid => LocalizedText.Get(
        "Enter a colour like #0a5cad (3 or 6 hex digits).",
        "أدخل لوناً بصيغة مثل #0a5cad (3 أو 6 خانات سداسية عشرية).");

    public static string FileRequired => LocalizedText.Get("Choose a logo file.", "اختر ملف الشعار.");

    public static string FileTooLarge => LocalizedText.Get(
        "The logo must be 2 MB or smaller.",
        "يجب ألا يتجاوز حجم الشعار 2 ميغابايت.");

    public static string FileTypeInvalid => LocalizedText.Get(
        "The logo must be a PNG, JPG, WEBP or GIF image.",
        "يجب أن يكون الشعار صورة PNG أو JPG أو WEBP أو GIF.");
}
