using Crm.Application.Common.Localization;

namespace Crm.Application.Reports;

/// <summary>User-facing text of the reports feature, in the request language.</summary>
public static class ReportText
{
    public static string FromAfterTo => LocalizedText.Get(
        "The start date must not be after the end date.", "يجب ألا يكون تاريخ البداية بعد تاريخ النهاية.");

    public static string RangeTooLong => LocalizedText.Get(
        "The date range can be at most 366 days.", "يجب ألا تزيد الفترة على 366 يوماً.");

    public static string StatusInvalid => LocalizedText.Get(
        "Choose new, open, pending, resolved or closed.", "اختر جديدة أو مفتوحة أو معلّقة أو محلولة أو مغلقة.");

    public static string ChannelInvalid => LocalizedText.Get(
        "Choose manual, email, whatsapp or portal.", "اختر يدوي أو بريد إلكتروني أو واتساب أو بوابة.");

    public static string PriorityInvalid => LocalizedText.Get(
        "Choose high, mid or low.", "اختر عالية أو متوسطة أو منخفضة.");

    public static string FormatInvalid => LocalizedText.Get(
        "Choose csv or xlsx.", "اختر csv أو xlsx.");

    // Export column headings and row labels.
    public static string ColumnReport => LocalizedText.Get("Report", "التقرير");

    public static string ColumnValue => LocalizedText.Get("Value", "القيمة");

    public static string ColumnCount => LocalizedText.Get("Count", "العدد");

    public static string SectionStatus => LocalizedText.Get("Status", "الحالة");

    public static string SectionCategory => LocalizedText.Get("Category", "الفئة");

    public static string SectionChannel => LocalizedText.Get("Channel", "القناة");

    public static string SectionPriority => LocalizedText.Get("Priority", "الأولوية");

    public static string SectionDay => LocalizedText.Get("Day", "اليوم");

    public static string Uncategorized => LocalizedText.Get("Uncategorized", "بدون فئة");
}
