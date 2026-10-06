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

    public static string StatusInvalid => LocalizedText.Get(
        "Choose new, open, pending, resolved or closed.",
        "اختر جديدة أو مفتوحة أو معلّقة أو محلولة أو مغلقة.");

    public static string StatusTargetInvalid => LocalizedText.Get(
        "Choose open, pending, resolved or closed.",
        "اختر مفتوحة أو معلّقة أو محلولة أو مغلقة.");

    public static string DateRangeInvalid => LocalizedText.Get(
        "The end date must not be before the start date.",
        "يجب ألا يكون تاريخ النهاية قبل تاريخ البداية.");

    public static string AssigneeOrUnassigned => LocalizedText.Get(
        "Filter by an assignee or by unassigned tickets, not both.",
        "صفِّ حسب موظف محدد أو حسب التذاكر غير المسندة، وليس كليهما.");

    public static string NumberTaken => LocalizedText.Get(
        "Another ticket was created at the same moment. Please try again.",
        "أُنشئت تذكرة أخرى في اللحظة نفسها. حاول مرة أخرى.");

    public static string AssigneeUnavailable => LocalizedText.Get(
        "Choose an active staff user.",
        "اختر موظفاً نشطاً.");

    public static string AgentNotFound => LocalizedText.Get(
        "The agent was not found.",
        "الموظف غير موجود.");

    public static string SettingRequired => LocalizedText.Get(
        "This value is required.",
        "هذه القيمة مطلوبة.");

    public static string AssignForbidden => LocalizedText.Get(
        "You may only assign a ticket to yourself.",
        "يمكنك إسناد التذكرة إلى نفسك فقط.");

    /// <summary>"A closed ticket can only move to: open."</summary>
    public static string StatusTransitionInvalid(string from, IEnumerable<string> allowed) => LocalizedText.Get(
        $"A {from} ticket can only move to: {string.Join(", ", allowed)}.",
        $"التذكرة في حالة {from} يمكن نقلها فقط إلى: {string.Join("، ", allowed)}.");

    public static string NotFound => LocalizedText.Get(
        "The ticket was not found.",
        "التذكرة غير موجودة.");
}
