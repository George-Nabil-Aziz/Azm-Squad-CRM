using Crm.Application.Common.Localization;

namespace Crm.Application.Tickets;

/// <summary>User-facing text of the tickets feature, in the request language.</summary>
public static class TicketText
{
    public static string CustomerField => LocalizedText.Get("Customer", "العميل");

    public static string SubjectField => LocalizedText.Get("Subject", "الموضوع");

    public static string DescriptionField => LocalizedText.Get("Description", "الوصف");

    public static string PriorityField => LocalizedText.Get("Priority", "الأولوية");

    public static string PriorityInvalid => LocalizedText.Get(
        "Choose high, mid or low.",
        "اختر عالية أو متوسطة أو منخفضة.");

    public static string CustomerNotFound => LocalizedText.Get(
        "The customer was not found.",
        "العميل غير موجود.");

    public static string CategoryUnavailable => LocalizedText.Get(
        "Choose an active category.",
        "اختر فئة نشطة.");

    public static string NumberTaken => LocalizedText.Get(
        "Another ticket was created at the same moment. Please try again.",
        "أُنشئت تذكرة أخرى في اللحظة نفسها. حاول مرة أخرى.");

    public static string NotFound => LocalizedText.Get(
        "The ticket was not found.",
        "التذكرة غير موجودة.");
}
