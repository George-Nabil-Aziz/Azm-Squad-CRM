using Crm.Application.Common.Localization;

namespace Crm.Application.Audit;

/// <summary>User-facing text of the audit-log feature, in the request language.</summary>
public static class AuditText
{
    public static string PageField => LocalizedText.Get("Page", "الصفحة");

    public static string PageSizeField => LocalizedText.Get("Page size", "حجم الصفحة");

    public static string UnknownAction => LocalizedText.Get("Unknown action.", "إجراء غير معروف.");

    public static string FromAfterTo => LocalizedText.Get(
        "The start date must not be after the end date.",
        "يجب ألا يكون تاريخ البداية بعد تاريخ النهاية.");
}
