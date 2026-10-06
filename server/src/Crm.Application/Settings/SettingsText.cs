using Crm.Application.Common.Localization;

namespace Crm.Application.Settings;

/// <summary>User-facing text of the system-configuration feature, in the request language.</summary>
public static class SettingsText
{
    public static string EnabledRequired => LocalizedText.Get(
        "Say whether business hours are on or off.", "حدد ما إذا كانت ساعات العمل مفعّلة أو معطّلة.");

    public static string DaysInvalid => LocalizedText.Get(
        "Working days must be day names from sunday to saturday.", "يجب أن تكون أيام العمل أسماء أيام من الأحد إلى السبت.");

    public static string DaysRequired => LocalizedText.Get(
        "Select at least one working day.", "اختر يوم عمل واحداً على الأقل.");

    public static string TimeInvalid => LocalizedText.Get(
        "Enter a time as HH:mm (for example 08:00).", "أدخل الوقت بصيغة HH:mm (مثال 08:00).");

    public static string EndBeforeStart => LocalizedText.Get(
        "The closing time must be after the opening time.", "يجب أن يكون وقت الإغلاق بعد وقت الفتح.");

    public static string TimeZoneInvalid => LocalizedText.Get(
        "This time zone is not known (use a name such as Asia/Riyadh).", "المنطقة الزمنية غير معروفة (استخدم اسماً مثل Asia/Riyadh).");

    public static string PrefixInvalid => LocalizedText.Get(
        "The prefix must be 1 to 10 letters or digits, optionally ending with a dash.",
        "يجب أن تتكون البادئة من 1 إلى 10 أحرف أو أرقام، وقد تنتهي بشرطة.");

    public static string EmailInvalid => LocalizedText.Get(
        "Enter a valid email address.", "أدخل عنوان بريد إلكتروني صحيحاً.");

    public static string PortInvalid => LocalizedText.Get(
        "The port must be a number from 1 to 65535.", "يجب أن يكون المنفذ رقماً من 1 إلى 65535.");

    public static string SecurityInvalid => LocalizedText.Get(
        "Choose None, Auto, StartTls or SslOnConnect.", "اختر None أو Auto أو StartTls أو SslOnConnect.");
}
