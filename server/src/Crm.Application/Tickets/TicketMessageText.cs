using Crm.Application.Common.Localization;

namespace Crm.Application.Tickets;

/// <summary>User-facing text of the ticket thread, in the request language.</summary>
public static class TicketMessageText
{
    public static string BodyField => LocalizedText.Get("Message", "الرسالة");

    public static string TicketClosed => LocalizedText.Get(
        "The ticket is closed. Reopen it to reply or add a note.",
        "التذكرة مغلقة. أعد فتحها للرد أو لإضافة ملاحظة.");
}
