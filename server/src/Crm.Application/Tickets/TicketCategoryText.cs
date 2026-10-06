using Crm.Application.Common.Localization;

namespace Crm.Application.Tickets;

/// <summary>User-facing text of the ticket categories feature, in the request language.</summary>
public static class TicketCategoryText
{
    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string NameTaken => LocalizedText.Get(
        "A category with this name already exists.",
        "توجد فئة بهذا الاسم بالفعل.");

    public static string NotFound => LocalizedText.Get(
        "The category was not found.",
        "الفئة غير موجودة.");
}
